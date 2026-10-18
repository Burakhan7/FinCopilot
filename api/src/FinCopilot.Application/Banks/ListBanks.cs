using FinCopilot.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FinCopilot.Application.Banks;

public sealed record ListBanksQuery : IRequest<IReadOnlyList<BankDto>>;

public sealed record BankDto(string HhsKod, string Name, string? LogoUrl);

public sealed class ListBanksHandler(IAppDbContext db) : IRequestHandler<ListBanksQuery, IReadOnlyList<BankDto>>
{
    public async Task<IReadOnlyList<BankDto>> Handle(ListBanksQuery request, CancellationToken ct) =>
        await db.Banks.AsNoTracking()
            .Where(b => b.IsActive)
            .OrderBy(b => b.Name)
            .Select(b => new BankDto(b.HhsKod, b.Name, b.LogoUrl))
            .ToListAsync(ct);
}
