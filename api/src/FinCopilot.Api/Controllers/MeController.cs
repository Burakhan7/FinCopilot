using FinCopilot.Api.Infrastructure;
using FinCopilot.Application.Users;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinCopilot.Api.Controllers;

[ApiController]
[Authorize]
[Route("me")]
public sealed class MeController(ISender mediator) : ControllerBase
{
    public sealed record SetTcknRequest(string Tckn);

    [HttpGet]
    public Task<MeDto> Get(CancellationToken ct) => mediator.Send(new GetMeQuery(User.GetUserId()), ct);

    /// <summary>İlk banka bağlamadan önce bir kez çağrılır.</summary>
    [HttpPut("tckn")]
    public async Task<IActionResult> SetTckn(SetTcknRequest request, CancellationToken ct)
    {
        await mediator.Send(new SetTcknCommand(User.GetUserId(), request.Tckn), ct);
        return NoContent();
    }
}
