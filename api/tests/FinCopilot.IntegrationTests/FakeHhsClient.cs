using System.Collections.Concurrent;
using FinCopilot.Application.Abstractions;

namespace FinCopilot.IntegrationTests;

/// <summary>
/// Bellek içi HHS: mock-hhs/app/consent_service.py'deki durum kurallarını taklit eder.
/// Testler bankadaki kullanıcı eylemlerini <see cref="Authorize"/> / <see cref="CancelAtBank"/> ile canlandırır.
/// </summary>
public sealed class FakeHhsClient : IHhsClient
{
    public sealed class State
    {
        public required HhsConsentRequest Request { get; set; }
        public string Drm { get; set; } = "B";
        public string? IptalKodu { get; set; }
        public string? YetKod { get; set; }
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }

        /// <summary>Bankada seçilmiş hesap/kart verisi (veri uçları buradan okur).</summary>
        public BankData Data { get; set; } = new();
        /// <summary>Veri uçlarına yapılan çağrılar (yol, PSU-Initiated, pencere).</summary>
        public ConcurrentQueue<(string Path, bool Psu, HhsWindow? Window)> Calls { get; } = new();
        /// <summary>Sıradaki veri çağrısında fırlatılacak hatalar.</summary>
        public ConcurrentQueue<Exception> NextErrors { get; } = new();
        internal ConcurrentDictionary<(string Path, DateOnly Day), int> SystemQueries { get; } = new();
    }

    public sealed class BankData
    {
        public List<HhsAccount> Accounts { get; init; } = [];
        public List<HhsBalance> Balances { get; init; } = [];
        public Dictionary<string, List<HhsTransaction>> Transactions { get; init; } = [];
        public List<HhsCard> Cards { get; init; } = [];
        public Dictionary<string, HhsCardDetail> CardDetails { get; init; } = [];
        public Dictionary<string, List<HhsCardTransaction>> CardTransactions { get; init; } = [];
    }

    public ConcurrentDictionary<string, State> Consents { get; } = new();
    public ConcurrentBag<(string RizaNo, string? AccessToken)> Revocations { get; } = [];

    /// <summary>Banka başına tek olay aboneliği (hhsKod → abonelik).</summary>
    public ConcurrentDictionary<string, HhsSubscription> Subscriptions { get; } = new();

    /// <summary>Kullanıcı bankanın kendi uygulamasında ayrık GKD isteğini onayladı: Y + yetKod (rıza sorgusunda döner).</summary>
    public void AuthorizeDecoupled(string rizaNo) => Authorize(rizaNo);

    /// <summary>Bankanın erişim belirtecini geçersiz kılar (süresi beklenenden önce bitmiş gibi).</summary>
    public void InvalidateAccessToken(string rizaNo) => Consents[rizaNo].AccessToken = "gecersiz-" + Guid.NewGuid().ToString("N");

    public string Authorize(string rizaNo)
    {
        var s = Consents[rizaNo];
        s.Drm = "Y";
        s.YetKod = Guid.NewGuid().ToString("N");
        return s.YetKod;
    }

    public void CancelAtBank(string rizaNo, string kod)
    {
        Consents[rizaNo].Drm = "I";
        Consents[rizaNo].IptalKodu = kod;
    }

    public Task<HhsConsent> CreateConsentAsync(HhsConsentRequest request, CancellationToken ct)
    {
        // mock-hhs validate_ayrik: ayrık GKD için AYRIK_GKD_* olaylarına abonelik şart.
        if (request.Decoupled && !(Subscriptions.TryGetValue(request.HhsKod, out var sub)
                && sub.Types.Any(t => t.OlayTipi == "AYRIK_GKD_BASARILI") && sub.Types.Any(t => t.OlayTipi == "AYRIK_GKD_BASARISIZ")))
            throw new HhsException(400, "TR.OHVPS.Field.Invalid", "Ayrık GKD için olay aboneliği gerekli.");

        foreach (var old in Consents.Values.Where(c => c.Drm == "B" && c.Request.Tckn == request.Tckn && c.Request.HhsKod == request.HhsKod))
            (old.Drm, old.IptalKodu) = ("I", "01");

        var rizaNo = Guid.NewGuid().ToString();
        Consents[rizaNo] = new State { Request = request };
        return Task.FromResult(Map(rizaNo));
    }

    public Task<HhsConsent> GetConsentAsync(string hhsKod, string rizaNo, CancellationToken ct) =>
        Consents.ContainsKey(rizaNo)
            ? Task.FromResult(Map(rizaNo))
            : throw new HhsException(404, "TR.OHVPS.Resource.NotFound", "Rıza bulunamadı.");

    public Task RevokeConsentAsync(string hhsKod, string rizaNo, string? accessToken, CancellationToken ct)
    {
        var s = Consents[rizaNo];
        if (s.Drm is "S" or "I")
            throw new HhsException(403, "TR.OHVPS.Resource.ConsentRevoked", "Rıza iptal edilmiş veya sonlanmış.");
        if (s.Drm == "K" && accessToken != s.AccessToken)
            throw new HhsException(401, "TR.OHVPS.Connection.InvalidToken", "Erişim belirteci geçersiz.");

        (s.Drm, s.IptalKodu) = ("I", "03");
        Revocations.Add((rizaNo, accessToken));
        return Task.CompletedTask;
    }

    public Task<HhsToken> ExchangeAuthCodeAsync(string hhsKod, string rizaNo, string yetKod, CancellationToken ct)
    {
        var s = Consents[rizaNo];
        if (s.Drm != "Y" || s.YetKod != yetKod)
            throw new HhsException(401, "TR.OHVPS.Connection.InvalidToken", "yetKod geçersiz.");

        s.Drm = "K";
        s.YetKod = null;
        s.AccessToken = "erisim-" + Guid.NewGuid().ToString("N");
        s.RefreshToken = "yenileme-" + Guid.NewGuid().ToString("N");
        return Task.FromResult(new HhsToken(s.AccessToken, TimeSpan.FromDays(1), s.RefreshToken, TimeSpan.FromDays(180)));
    }

    public Task<HhsToken> RefreshAccessTokenAsync(string hhsKod, string rizaNo, string refreshToken, CancellationToken ct)
    {
        var s = Consents[rizaNo];
        if (s.Drm != "K" || s.RefreshToken != refreshToken)
            throw new HhsException(401, "TR.OHVPS.Connection.InvalidToken", "yenilemeBelirteci geçersiz.");

        s.AccessToken = "erisim-" + Guid.NewGuid().ToString("N");
        return Task.FromResult(new HhsToken(s.AccessToken, TimeSpan.FromDays(1), s.RefreshToken, TimeSpan.FromDays(180)));
    }

    // ------------------------------------------------------------------ veri uçları (mock-hhs/app/routers/data.py kuralları)

    public Task<IReadOnlyList<HhsAccount>> GetAccountsAsync(HhsAccess access, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<HhsAccount>>(Data(access, "/hesaplar", null).Accounts);

    public Task<IReadOnlyList<HhsBalance>> GetBalancesAsync(HhsAccess access, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<HhsBalance>>(Data(access, "/bakiye", null).Balances);

    public Task<HhsBalance> GetBalanceAsync(HhsAccess access, string hspRef, CancellationToken ct) =>
        Data(access, $"/hesaplar/{hspRef}/bakiye", null).Balances.FirstOrDefault(b => b.HspRef == hspRef) is { } b
            ? Task.FromResult(b)
            : throw new HhsException(404, "TR.OHVPS.Resource.NotFound", "Kaynak bulunamadı.");

    public Task<HhsSubscription?> GetSubscriptionAsync(string hhsKod, CancellationToken ct) =>
        Task.FromResult(Subscriptions.GetValueOrDefault(hhsKod));

    public Task<HhsSubscription> CreateSubscriptionAsync(string hhsKod, IReadOnlyList<HhsEventType> types, CancellationToken ct)
    {
        var sub = new HhsSubscription(Guid.NewGuid().ToString(), types);
        if (!Subscriptions.TryAdd(hhsKod, sub))
            throw new HhsException(400, "TR.OHVPS.Business.EventSubscriptionAlreadyExists", "Bu HHS'de zaten bir olay aboneliği var.");
        return Task.FromResult(sub);
    }

    public Task<HhsSubscription> UpdateSubscriptionAsync(string hhsKod, string olayAbonelikNo, IReadOnlyList<HhsEventType> types, CancellationToken ct)
    {
        if (!Subscriptions.TryGetValue(hhsKod, out var current) || current.OlayAbonelikNo != olayAbonelikNo)
            throw new HhsException(404, "TR.OHVPS.Resource.NotFound", "Olay aboneliği bulunamadı.");
        return Task.FromResult(Subscriptions[hhsKod] = current with { Types = types });
    }

    public Task<HhsPage<HhsTransaction>> GetTransactionsAsync(HhsAccess access, string hspRef, HhsWindow window, int page, int pageSize, CancellationToken ct)
    {
        var items = Data(access, $"/hesaplar/{hspRef}/islemler", window).Transactions.GetValueOrDefault(hspRef, []);
        return Task.FromResult(Page(items.Where(t => t.Zaman >= window.Start && t.Zaman <= window.End).OrderByDescending(t => t.Zaman), page, pageSize));
    }

    public Task<IReadOnlyList<HhsCard>> GetCardsAsync(HhsAccess access, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<HhsCard>>(Data(access, "/kartlar", null).Cards);

    public Task<HhsCardDetail> GetCardDetailAsync(HhsAccess access, string kartRef, CancellationToken ct) =>
        Data(access, $"/kartlar/{kartRef}/kart-detay", null).CardDetails.TryGetValue(kartRef, out var d)
            ? Task.FromResult(d)
            : throw new HhsException(404, "TR.OHVPS.Resource.NotFound", "Bu kart için kredi kartı detayı bulunmuyor.");

    public Task<HhsPage<HhsCardTransaction>> GetCardTransactionsAsync(HhsAccess access, string kartRef, HhsWindow window, int page, int pageSize, CancellationToken ct)
    {
        var data = Data(access, $"/kartlar/{kartRef}/kart-hareketleri", window);
        if (!data.CardTransactions.TryGetValue(kartRef, out var items))
            throw new HhsException(404, "TR.OHVPS.Resource.NotFound", "Bu kart için kredi kartı detayı bulunmuyor.");
        return Task.FromResult(Page(items.Where(m => m.Tarih >= window.Start && m.Tarih <= window.End).OrderByDescending(m => m.Tarih), page, pageSize));
    }

    /// <summary>Token, rıza durumu, pencere ve kota kontrolleri; çağrıyı kaydeder.</summary>
    private BankData Data(HhsAccess access, string path, HhsWindow? window)
    {
        var s = Consents.Values.FirstOrDefault(c => c.AccessToken == access.AccessToken)
            ?? throw new HhsException(401, "TR.OHVPS.Connection.InvalidToken", "Erişim belirteci geçersiz veya süresi dolmuş.");
        s.Calls.Enqueue((path, access.PsuInitiated, window));

        if (s.NextErrors.TryDequeue(out var error))
            throw error;
        if (s.Drm != "K")
            throw new HhsException(403, "TR.OHVPS.Resource.ConsentRevoked", "Rıza iptal edilmiş veya sonlanmış.");

        if (window is not null)
        {
            var limit = access.PsuInitiated ? TimeSpan.FromDays(31) : TimeSpan.FromHours(24);
            if (window.End - window.Start > limit || window.Start < s.Request.HesapIslemBslZmn)
                throw new HhsException(400, "TR.OHVPS.Business.InvalidStartEndTime", "Sorgu aralığı geçersiz.");
        }

        if (!access.PsuInitiated)
        {
            var day = DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(3)).DateTime);
            var used = s.SystemQueries.AddOrUpdate((path, day), 1, (_, n) => n + 1);
            if (used > 4)
                throw new HhsException(429, "TR.OHVPS.Connection.ExceededRate", "Sistem tetikli sorgu kotası aşıldı.");
        }
        return s.Data;
    }

    private static HhsPage<T> Page<T>(IEnumerable<T> items, int page, int size)
    {
        var all = items.ToList();
        return new HhsPage<T>(all.Skip((page - 1) * size).Take(size).ToList(), all.Count, Math.Max(1, (all.Count + size - 1) / size));
    }

    private HhsConsent Map(string rizaNo)
    {
        var s = Consents[rizaNo];
        var decoupled = s.Request.Decoupled;
        return new HhsConsent(rizaNo, s.Drm, s.IptalKodu, decoupled ? null : $"http://fake-bank.test/gkd/{rizaNo}",
            s.Request.IzinTurleri, s.Request.ErisimIzniSonTrh, s.Request.HesapIslemBslZmn, s.Request.HesapIslemBtsZmn,
            YetKod: decoupled && s.Drm == "Y" ? s.YetKod : null);
    }
}
