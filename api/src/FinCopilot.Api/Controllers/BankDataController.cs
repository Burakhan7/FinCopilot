using FinCopilot.Api.Infrastructure;
using FinCopilot.Application.BankData;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinCopilot.Api.Controllers;

[ApiController]
[Authorize]
public sealed class BankDataController(ISender mediator) : ControllerBase
{
    [HttpGet("accounts")]
    public Task<IReadOnlyList<AccountDto>> Accounts(CancellationToken ct) =>
        mediator.Send(new ListAccountsQuery(User.GetUserId()), ct);

    [HttpGet("cards")]
    public Task<IReadOnlyList<CardDto>> Cards(CancellationToken ct) =>
        mediator.Send(new ListCardsQuery(User.GetUserId()), ct);
}
