using System.Text.Json;
using FinCopilot.Application.Events;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinCopilot.Api.Controllers;

/// <summary>
/// YÖS'ün açması zorunlu olay uçları (HHS çağırır, 202 bekler). Kimlik doğrulaması yok: olay içeriğine güvenilmez,
/// veri her zaman HHS'den tekrar okunur. ⚠ Gerçek ortamda X-JWS-Signature doğrulanmalı (README).
/// </summary>
[ApiController]
[AllowAnonymous]
public sealed class EventsController(ISender mediator) : ControllerBase
{
    public sealed record KatilimciBlg(string HhsKod, string YosKod);

    public sealed record OlayBildirimi(
        string OlayNo,
        string? OlayAbonelikNo,
        string OlayTipi,
        string KaynakTipi,
        string KaynakNo,
        DateTimeOffset? OlayZamani,
        KatilimciBlg KatilimciBlg);

    [HttpPost("olay-dinleme")]
    public async Task<IActionResult> Olay(OlayBildirimi olay, CancellationToken ct)
    {
        await mediator.Send(new ReceiveEventCommand(
            olay.OlayNo, olay.OlayTipi, olay.KaynakTipi, olay.KaynakNo,
            olay.KatilimciBlg.HhsKod, olay.KatilimciBlg.YosKod,
            JsonSerializer.Serialize(olay, JsonSerializerOptions.Web)), ct);
        return Accepted();
    }

    [HttpPost("sistem-olay-dinleme")]
    public async Task<IActionResult> SistemOlay(JsonElement payload, CancellationToken ct)
    {
        await mediator.Send(new ReceiveSystemEventCommand(payload), ct);
        return Accepted();
    }
}
