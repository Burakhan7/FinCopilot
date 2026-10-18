using FinCopilot.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FinCopilot.Application.BankData;

public sealed record BalanceDto(decimal BkyTtr, decimal? KulKrdTtr, string ParaBirimi, DateTimeOffset BkyZmn);

public sealed record AccountDto(
    Guid Id,
    string HhsKod,
    string BankName,
    string? HspNoMasked,
    string? Name,
    string ParaBirimi,
    string? HspTip,
    BalanceDto? Balance);

/// <summary>Kullanıcının aktif hesapları, en son bakiyeleriyle.</summary>
public sealed record ListAccountsQuery(Guid UserId) : IRequest<IReadOnlyList<AccountDto>>;

public sealed class ListAccountsHandler(IAppDbContext db) : IRequestHandler<ListAccountsQuery, IReadOnlyList<AccountDto>>
{
    public async Task<IReadOnlyList<AccountDto>> Handle(ListAccountsQuery request, CancellationToken ct) =>
        await (
            from a in db.Accounts.AsNoTracking()
            join b in db.Banks on a.HhsKod equals b.HhsKod
            where a.UserId == request.UserId && a.IsActive
            orderby b.Name, a.KisaAd
            let last = db.BalanceSnapshots.Where(s => s.AccountId == a.Id).OrderByDescending(s => s.BkyZmn).FirstOrDefault()
            select new AccountDto(
                a.Id, a.HhsKod, b.Name, a.HspNoMasked, a.KisaAd ?? a.UrunAdi, a.ParaBirimi, a.HspTip,
                last == null ? null : new BalanceDto(last.BkyTtr, last.KulKrdTtr, last.ParaBirimi, last.BkyZmn))
        ).ToListAsync(ct);
}

public sealed record StatementDto(
    DateOnly StatementDate,
    DateOnly DueDate,
    decimal PeriodDebt,
    decimal MinPayment,
    decimal? CreditLimit,
    decimal? UsedLimit);

/// <param name="KartTipi">B banka, K kredi, P ön ödemeli.</param>
public sealed record CardDto(
    Guid Id,
    string HhsKod,
    string BankName,
    string? SonNo,
    string KartTipi,
    string? Name,
    StatementDto? LastStatement);

/// <summary>Kullanıcının aktif kartları; kredi kartlarında son ekstreyle.</summary>
public sealed record ListCardsQuery(Guid UserId) : IRequest<IReadOnlyList<CardDto>>;

public sealed class ListCardsHandler(IAppDbContext db) : IRequestHandler<ListCardsQuery, IReadOnlyList<CardDto>>
{
    public async Task<IReadOnlyList<CardDto>> Handle(ListCardsQuery request, CancellationToken ct) =>
        await (
            from c in db.Cards.AsNoTracking()
            join b in db.Banks on c.HhsKod equals b.HhsKod
            where c.UserId == request.UserId && c.IsActive
            orderby b.Name, c.UrunAdi
            let last = db.CardStatements.Where(s => s.CardId == c.Id).OrderByDescending(s => s.StatementDate).FirstOrDefault()
            select new CardDto(
                c.Id, c.HhsKod, b.Name, c.SonNo, c.KartTipi, c.UrunAdi,
                last == null ? null : new StatementDto(last.StatementDate, last.DueDate, last.PeriodDebt, last.MinPayment, last.CreditLimit, last.UsedLimit))
        ).ToListAsync(ct);
}
