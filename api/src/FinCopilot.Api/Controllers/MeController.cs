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
    [HttpGet]
    public Task<MeDto> Get(CancellationToken ct) => mediator.Send(new GetMeQuery(User.GetUserId()), ct);
}
