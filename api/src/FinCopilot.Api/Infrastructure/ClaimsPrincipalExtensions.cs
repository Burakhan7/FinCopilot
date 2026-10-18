using System.Security.Claims;
using FinCopilot.Infrastructure.Auth;

namespace FinCopilot.Api.Infrastructure;

internal static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(AppClaims.UserId)!);

    public static Guid GetSessionId(this ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(AppClaims.SessionId)!);
}
