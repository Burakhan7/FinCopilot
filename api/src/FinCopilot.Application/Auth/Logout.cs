using FinCopilot.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FinCopilot.Application.Auth;

public sealed record LogoutCommand(Guid UserId, Guid SessionId) : IRequest;

public sealed class LogoutHandler(IAppDbContext db, TimeProvider clock) : IRequestHandler<LogoutCommand>
{
    public async Task Handle(LogoutCommand request, CancellationToken ct)
    {
        var session = await db.UserSessions.FirstOrDefaultAsync(
            s => s.Id == request.SessionId && s.UserId == request.UserId && s.RevokedAt == null, ct);
        if (session is null)
            return;

        SessionIssuer.Revoke(session, clock.GetUtcNow(), "logout");
        await db.SaveChangesAsync(ct);
    }
}
