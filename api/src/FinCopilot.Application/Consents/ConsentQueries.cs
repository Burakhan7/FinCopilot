using FinCopilot.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FinCopilot.Application.Consents;

/// <summary>Kullanıcının banka bağlantıları (yerel veriden, HHS'ye gitmeden).</summary>
public sealed record ListConsentsQuery(Guid UserId) : IRequest<IReadOnlyList<ConsentDto>>;

public sealed class ListConsentsHandler(IAppDbContext db) : IRequestHandler<ListConsentsQuery, IReadOnlyList<ConsentDto>>
{
    public async Task<IReadOnlyList<ConsentDto>> Handle(ListConsentsQuery request, CancellationToken ct)
    {
        var rows = await (
            from c in db.Consents.AsNoTracking()
            join b in db.Banks on c.HhsKod equals b.HhsKod
            where c.UserId == request.UserId
            orderby c.CreatedAt descending
            select new { Consent = c, BankName = b.Name }).ToListAsync(ct);

        var syncs = await ConsentService.LastSyncsAsync(db, rows.Select(r => r.Consent.Id).ToList(), ct);
        return rows.Select(r => ConsentService.ToDto(r.Consent, r.BankName, syncs.GetValueOrDefault(r.Consent.Id))).ToList();
    }
}

/// <summary>Tek bağlantı; aktifse durumu HHS'den tazelenir.</summary>
public sealed record GetConsentQuery(Guid UserId, Guid ConsentId) : IRequest<ConsentDto>;

/// <remarks>Ayrık GKD'de olay gelmezse (veya gecikirse) bağlantı burada tamamlanır.</remarks>
public sealed class GetConsentHandler(IAppDbContext db, ConsentService consents, ISyncJobScheduler jobs)
    : IRequestHandler<GetConsentQuery, ConsentDto>
{
    public async Task<ConsentDto> Handle(GetConsentQuery request, CancellationToken ct)
    {
        var consent = await consents.GetOwnedAsync(request.UserId, request.ConsentId, ct);
        var connected = await consents.SyncStatusAsync(consent, ct);
        await db.SaveChangesAsync(ct);
        if (connected)
            jobs.EnqueueBackfill(consent.Id);
        return await consents.ToDtoAsync(consent, ct);
    }
}
