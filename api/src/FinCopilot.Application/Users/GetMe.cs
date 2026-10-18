using FinCopilot.Application.Abstractions;
using FinCopilot.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FinCopilot.Application.Users;

public sealed record GetMeQuery(Guid UserId) : IRequest<MeDto>;

public sealed record MeDto(Guid Id, string Phone, string? FullName, bool HasTckn, DateTimeOffset CreatedAt);

public sealed class GetMeHandler(IAppDbContext db) : IRequestHandler<GetMeQuery, MeDto>
{
    public async Task<MeDto> Handle(GetMeQuery request, CancellationToken ct) =>
        await db.Users.AsNoTracking()
            .Where(u => u.Id == request.UserId)
            .Select(u => new MeDto(u.Id, u.Phone, u.FullName, u.TcknEnc != null, u.CreatedAt))
            .FirstOrDefaultAsync(ct)
        ?? throw AppProblemException.Unauthorized("user_not_found", "Kullanıcı bulunamadı.");
}
