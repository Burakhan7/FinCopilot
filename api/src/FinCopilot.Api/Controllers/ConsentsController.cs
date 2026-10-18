using FinCopilot.Api.Infrastructure;
using FinCopilot.Application.Banks;
using FinCopilot.Application.Consents;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinCopilot.Api.Controllers;

[ApiController]
[Authorize]
[Route("banks")]
public sealed class BanksController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<BankDto>> List(CancellationToken ct) => mediator.Send(new ListBanksQuery(), ct);
}

[ApiController]
[Authorize]
[Route("consents")]
public sealed class ConsentsController(ISender mediator) : ControllerBase
{
    public sealed record CreateConsentRequest(string HhsKod);

    /// <summary>Bankanın deep link ile döndürdüğü parametreler (yetKod sadece onayda gelir).</summary>
    public sealed record GkdCallbackRequest(string RizaNo, string DrmKod, string? YetKod);

    [HttpGet]
    public Task<IReadOnlyList<ConsentDto>> List(CancellationToken ct) =>
        mediator.Send(new ListConsentsQuery(User.GetUserId()), ct);

    [HttpGet("{id:guid}")]
    public Task<ConsentDto> Get(Guid id, CancellationToken ct) =>
        mediator.Send(new GetConsentQuery(User.GetUserId(), id), ct);

    [HttpPost]
    public async Task<ActionResult<CreateConsentResult>> Create(CreateConsentRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new CreateConsentCommand(User.GetUserId(), request.HhsKod), ct);
        return CreatedAtAction(nameof(Get), new { id = result.ConsentId }, result);
    }

    [HttpPost("{id:guid}/callback")]
    public Task<ConsentDto> Callback(Guid id, GkdCallbackRequest request, CancellationToken ct) =>
        mediator.Send(new CompleteGkdCommand(User.GetUserId(), id, request.RizaNo, request.DrmKod, request.YetKod), ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
    {
        await mediator.Send(new RevokeConsentCommand(User.GetUserId(), id), ct);
        return NoContent();
    }
}
