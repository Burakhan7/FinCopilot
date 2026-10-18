using FinCopilot.Api.Infrastructure;
using FinCopilot.Application.Auth;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinCopilot.Api.Controllers;

[ApiController]
[Route("auth")]
public sealed class AuthController(ISender mediator) : ControllerBase
{
    /// <summary>Telefona SMS kodu gönderir (kayıt ve yeni cihaz doğrulaması için).</summary>
    [HttpPost("otp")]
    public Task<RequestOtpResult> RequestOtp(RequestOtpCommand command, CancellationToken ct) => mediator.Send(command, ct);

    [HttpPost("register")]
    public Task<AuthTokens> Register(RegisterCommand command, CancellationToken ct) => mediator.Send(command, ct);

    [HttpPost("login")]
    public Task<AuthTokens> Login(LoginCommand command, CancellationToken ct) => mediator.Send(command, ct);

    [HttpPost("refresh")]
    public Task<AuthTokens> Refresh(RefreshTokenCommand command, CancellationToken ct) => mediator.Send(command, ct);

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await mediator.Send(new LogoutCommand(User.GetUserId(), User.GetSessionId()), ct);
        return NoContent();
    }
}
