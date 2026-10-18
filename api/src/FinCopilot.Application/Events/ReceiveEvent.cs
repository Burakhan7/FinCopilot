using System.Text.Json;
using FinCopilot.Application.Abstractions;
using FinCopilot.Application.Common;
using FinCopilot.Application.Consents;
using FinCopilot.Domain.Consents;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FinCopilot.Application.Events;

/// <summary>
/// HHS'nin POST /olay-dinleme çağrısı. Olay içeriğine güvenilmez: sadece kaydedilir ve kuyruğa atılır,
/// işlenirken veri her zaman HHS'den tekrar okunur. olayNo ile idempotent (HHS aynı olayı 3 kez deneyebilir).
/// </summary>
public sealed record ReceiveEventCommand(
    string OlayNo,
    string OlayTipi,
    string KaynakTipi,
    string KaynakNo,
    string HhsKod,
    string YosKod,
    string RawPayload) : IRequest;

public sealed class ReceiveEventValidator : AbstractValidator<ReceiveEventCommand>
{
    public ReceiveEventValidator()
    {
        RuleFor(x => x.OlayNo).NotEmpty().MaximumLength(64);
        RuleFor(x => x.OlayTipi).NotEmpty().MaximumLength(64);
        RuleFor(x => x.KaynakTipi).NotEmpty().MaximumLength(64);
        RuleFor(x => x.KaynakNo).NotEmpty().MaximumLength(64);
        RuleFor(x => x.HhsKod).NotEmpty().MaximumLength(8);
        RuleFor(x => x.YosKod).NotEmpty().MaximumLength(8);
    }
}

public sealed class ReceiveEventHandler(IAppDbContext db, ISyncJobScheduler jobs, TimeProvider clock, IOptions<HhsOptions> options)
    : IRequestHandler<ReceiveEventCommand>
{
    public async Task Handle(ReceiveEventCommand request, CancellationToken ct)
    {
        if (request.YosKod != options.Value.YosKod)
            throw AppProblemException.BadRequest("invalid_tpp", "Olay bu YÖS'e ait değil.");
        if (!await db.Banks.AnyAsync(b => b.HhsKod == request.HhsKod && b.IsActive, ct))
            throw AppProblemException.BadRequest("invalid_aspsp", "Tanımsız banka.");

        if (await db.EventInbox.AnyAsync(e => e.OlayNo == request.OlayNo, ct))
            return; // tekrar gönderim

        var item = new EventInboxItem
        {
            Id = Guid.NewGuid(),
            OlayNo = request.OlayNo,
            OlayTipi = request.OlayTipi,
            KaynakTipi = request.KaynakTipi,
            KaynakNo = request.KaynakNo,
            Payload = request.RawPayload,
            ReceivedAt = clock.GetUtcNow(),
        };
        db.EventInbox.Add(item);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Aynı olay eşzamanlı geldiyse diğer istek kaydetti (olay_no tekil); başka bir hataysa yükselt.
            db.EventInbox.Remove(item);
            if (await db.EventInbox.AnyAsync(e => e.OlayNo == request.OlayNo, CancellationToken.None))
                return;
            throw;
        }

        jobs.EnqueueEventProcessing(item.Id);
    }
}

/// <summary>
/// HHS'nin POST /sistem-olay-dinleme çağrısı (örn. HHS_YOS_GUNCELLENDI). Zorunlu uç; şimdilik sadece kaydedilir.
/// ⚠ Gövde şeması spesifikasyondan doğrulanmadı; Mock HHS bu olayları göndermiyor.
/// </summary>
public sealed record ReceiveSystemEventCommand(JsonElement Payload) : IRequest;

public sealed class ReceiveSystemEventHandler(IAppDbContext db, TimeProvider clock) : IRequestHandler<ReceiveSystemEventCommand>
{
    public async Task Handle(ReceiveSystemEventCommand request, CancellationToken ct)
    {
        string? Field(string name) =>
            request.Payload.ValueKind == JsonValueKind.Object && request.Payload.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
                ? v.GetString() : null;

        var olayNo = Field("olayNo") ?? Guid.NewGuid().ToString();
        if (await db.EventInbox.AnyAsync(e => e.OlayNo == olayNo, ct))
            return;

        var now = clock.GetUtcNow();
        db.EventInbox.Add(new EventInboxItem
        {
            Id = Guid.NewGuid(),
            OlayNo = Masking(olayNo, 64),
            OlayTipi = Masking(Field("olayTipi") ?? "SISTEM", 64),
            KaynakTipi = Field("kaynakTipi") is { } k ? Masking(k, 64) : null,
            KaynakNo = Field("kaynakNo") is { } n ? Masking(n, 64) : null,
            Payload = request.Payload.GetRawText(),
            ReceivedAt = now,
            ProcessedAt = now,
        });
        await db.SaveChangesAsync(ct);
    }

    private static string Masking(string value, int max) => Sync.Masking.Truncate(value, max)!;
}
