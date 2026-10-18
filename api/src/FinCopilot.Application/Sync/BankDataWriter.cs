using System.Text.Json;
using FinCopilot.Application.Abstractions;
using FinCopilot.Domain.BankData;
using FinCopilot.Domain.Consents;
using Microsoft.EntityFrameworkCore;

namespace FinCopilot.Application.Sync;

/// <summary>
/// HHS verisini tablolara yazar. Tekillik ÖHVPS anahtarlarıyla (hsp_ref, isl_no, kart_ref, islem_no):
/// aynı veri iki kez gelirse tekrar eklenmez, bu yüzden senkron güvenle yeniden çalıştırılabilir.
/// Her metot kendi değişikliklerini kaydeder (uzun ilk yüklemede change tracker şişmesin).
/// </summary>
public sealed class BankDataWriter(IAppDbContext db, ISecretProtector protector, TimeProvider clock)
{
    /// <summary>Hesapları ekler/günceller. HHS'nin artık döndürmediği hesaplar pasifleşir (kullanıcı bankada seçimi kaldırmış).</summary>
    public async Task<IReadOnlyList<Account>> UpsertAccountsAsync(Consent consent, IReadOnlyList<HhsAccount> items, CancellationToken ct)
    {
        var existing = await db.Accounts.Where(a => a.ConsentId == consent.Id).ToDictionaryAsync(a => a.HspRef, ct);

        foreach (var h in items)
        {
            if (!existing.TryGetValue(h.HspRef, out var a))
            {
                a = new Account { Id = Guid.NewGuid(), UserId = consent.UserId, ConsentId = consent.Id, HhsKod = consent.HhsKod, HspRef = h.HspRef, ParaBirimi = h.ParaBirimi };
                db.Accounts.Add(a);
                existing[h.HspRef] = a;
            }
            a.HspNoEnc = h.HspNo is null ? null : protector.Protect(h.HspNo, SecretPurpose.AccountNumber);
            a.HspNoMasked = Masking.Iban(h.HspNo);
            a.HspShb = Masking.Truncate(h.HspShb, 64);
            a.SubeAdi = Masking.Truncate(h.SubeAdi, 128);
            a.KisaAd = Masking.Truncate(h.KisaAd, 128);
            a.UrunAdi = Masking.Truncate(h.UrunAdi, 128);
            a.ParaBirimi = h.ParaBirimi;
            a.HspTur = h.HspTur;
            a.HspTip = h.HspTip;
            a.HspDrm = h.HspDrm;
            a.AcilisTrh = h.AcilisTrh ?? a.AcilisTrh;
            a.IsActive = true;
        }

        var returned = items.Select(i => i.HspRef).ToHashSet();
        foreach (var gone in existing.Values.Where(a => !returned.Contains(a.HspRef)))
            gone.IsActive = false;

        await db.SaveChangesAsync(ct);
        return existing.Values.Where(a => a.IsActive).ToList();
    }

    public async Task<int> InsertBalancesAsync(IReadOnlyList<Account> accounts, IReadOnlyList<HhsBalance> items, string source, CancellationToken ct)
    {
        var byRef = accounts.ToDictionary(a => a.HspRef);
        var added = 0;
        foreach (var b in items)
        {
            if (!byRef.TryGetValue(b.HspRef, out var account))
                continue;
            if (await db.BalanceSnapshots.AnyAsync(s => s.AccountId == account.Id && s.BkyZmn == b.BkyZmn, ct))
                continue;

            db.BalanceSnapshots.Add(new BalanceSnapshot
            {
                AccountId = account.Id,
                BkyTtr = b.BkyTtr,
                BlkTtr = b.BlkTtr,
                ParaBirimi = b.ParaBirimi,
                KulKrdTtr = b.KulKrdTtr,
                KrdDhlGstr = b.KrdDhlGstr,
                BkyZmn = b.BkyZmn,
                Source = source,
            });
            added++;
        }
        await db.SaveChangesAsync(ct);
        return added;
    }

    public async Task<int> InsertTransactionsAsync(Account account, IReadOnlyList<HhsTransaction> items, CancellationToken ct)
    {
        var fresh = await NewOnlyAsync(items, t => t.IslNo,
            keys => db.Transactions.Where(t => t.AccountId == account.Id && keys.Contains(t.IslNo)).Select(t => t.IslNo), ct);

        foreach (var t in fresh)
        {
            db.Transactions.Add(new Transaction
            {
                AccountId = account.Id,
                IslNo = t.IslNo,
                RefNo = t.RefNo,
                Amount = t.Tutar,
                Currency = t.ParaBirimi,
                Direction = t.BrcAlc,
                IslTur = t.IslTur,
                IslAmc = t.IslAmc,
                Kanal = t.Kanal,
                DescriptionRaw = Masking.Truncate(t.Aciklama, 512),
                OhkMsj = Masking.Truncate(t.OhkMsj, 512),
                CounterpartyName = Masking.Truncate(t.KarsiUnvan, 200),
                CounterpartyIbanMasked = Masking.Iban(t.KarsiIban),
                OccurredAt = t.Zaman,
            });
        }
        await db.SaveChangesAsync(ct);
        return fresh.Count;
    }

    public async Task<IReadOnlyList<Card>> UpsertCardsAsync(Consent consent, IReadOnlyList<HhsCard> items, CancellationToken ct)
    {
        var existing = await db.Cards.Where(c => c.ConsentId == consent.Id).ToDictionaryAsync(c => c.KartRef, ct);

        foreach (var h in items)
        {
            if (!existing.TryGetValue(h.KartRef, out var c))
            {
                c = new Card { Id = Guid.NewGuid(), UserId = consent.UserId, ConsentId = consent.Id, HhsKod = consent.HhsKod, KartRef = h.KartRef, KartTipi = h.KartTipi };
                db.Cards.Add(c);
                existing[h.KartRef] = c;
            }
            c.SonNo = h.SonNo;
            c.KartTipi = h.KartTipi;
            c.AltKartTipi = h.AltKartTipi;
            c.KartFormu = h.KartFormu;
            c.KartTuru = h.KartTuru;
            c.KartStatu = h.KartStatu;
            c.KartSema = h.KartSema;
            c.UrunAdi = Masking.Truncate(h.UrunAdi, 128);
            c.IsActive = true;
        }

        var returned = items.Select(i => i.KartRef).ToHashSet();
        foreach (var gone in existing.Values.Where(c => !returned.Contains(c.KartRef)))
            gone.IsActive = false;

        await db.SaveChangesAsync(ct);
        return existing.Values.Where(c => c.IsActive).ToList();
    }

    /// <summary>Son kesilen ekstre; (kart, kesim tarihi) ile tekil. Henüz ekstre kesilmemişse yazılmaz.</summary>
    public async Task UpsertStatementAsync(Card card, HhsCardDetail d, CancellationToken ct)
    {
        if (d.HesapKesimTarihi is not { } kesim || d.SonOdemeTarihi is not { } sonOdeme)
            return;

        var s = await db.CardStatements.FirstOrDefaultAsync(x => x.CardId == card.Id && x.StatementDate == kesim, ct);
        if (s is null)
        {
            s = new CardStatement { CardId = card.Id, StatementDate = kesim, Currency = d.ParaBirimi };
            db.CardStatements.Add(s);
        }
        s.DueDate = sonOdeme;
        s.PeriodDebt = d.DonemBorcu;
        s.MinPayment = d.AsgariOdeme;
        s.CreditLimit = d.Limit;
        s.UsedLimit = d.KullanilanLimit;
        s.Currency = d.ParaBirimi;
        s.FetchedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
    }

    public async Task<int> InsertCardTransactionsAsync(Card card, IReadOnlyList<HhsCardTransaction> items, CancellationToken ct)
    {
        var fresh = await NewOnlyAsync(items, m => m.IslemNo,
            keys => db.CardTransactions.Where(m => m.CardId == card.Id && keys.Contains(m.IslemNo)).Select(m => m.IslemNo), ct);

        foreach (var m in fresh)
        {
            db.CardTransactions.Add(new CardTransaction
            {
                CardId = card.Id,
                IslemNo = m.IslemNo,
                OccurredAt = m.Tarih,
                Amount = m.Tutar,
                Currency = m.ParaBirimi,
                Direction = m.BrcAlc,
                InstallmentCount = m.TaksitSayisi ?? 1,
                IslTuru = m.IslemTuru,
                MerchantRaw = Masking.Truncate(m.UyeIsyeri, 256),
                DescriptionRaw = Masking.Truncate(m.Aciklama, 512),
                FxInfo = m.Doviz is null ? null : JsonSerializer.Serialize(m.Doviz, JsonSerializerOptions.Web),
            });
        }
        await db.SaveChangesAsync(ct);
        return fresh.Count;
    }

    /// <summary>Hem veritabanında hem de aynı parti içinde tekrar edenleri ayıklar.</summary>
    private static async Task<List<T>> NewOnlyAsync<T>(
        IReadOnlyList<T> items, Func<T, string> key, Func<List<string>, IQueryable<string>> existingQuery, CancellationToken ct)
    {
        if (items.Count == 0)
            return [];
        var keys = items.Select(key).Distinct().ToList();
        var existing = (await existingQuery(keys).ToListAsync(ct)).ToHashSet();
        return items.Where(i => existing.Add(key(i))).ToList();
    }
}
