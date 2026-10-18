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
        public required HhsConsentRequest Request { get; init; }
        public string Drm { get; set; } = "B";
        public string? IptalKodu { get; set; }
        public string? YetKod { get; set; }
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
    }

    public ConcurrentDictionary<string, State> Consents { get; } = new();
    public ConcurrentBag<(string RizaNo, string? AccessToken)> Revocations { get; } = [];

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

    // Veri uçları senkronizasyon testleriyle (Faz 2, 5. adım) doldurulacak.
    public Task<IReadOnlyList<HhsAccount>> GetAccountsAsync(HhsAccess access, CancellationToken ct) => throw new NotSupportedException();
    public Task<IReadOnlyList<HhsBalance>> GetBalancesAsync(HhsAccess access, CancellationToken ct) => throw new NotSupportedException();
    public Task<HhsPage<HhsTransaction>> GetTransactionsAsync(HhsAccess access, string hspRef, HhsWindow window, int page, int pageSize, CancellationToken ct) => throw new NotSupportedException();
    public Task<IReadOnlyList<HhsCard>> GetCardsAsync(HhsAccess access, CancellationToken ct) => throw new NotSupportedException();
    public Task<HhsCardDetail> GetCardDetailAsync(HhsAccess access, string kartRef, CancellationToken ct) => throw new NotSupportedException();
    public Task<HhsPage<HhsCardTransaction>> GetCardTransactionsAsync(HhsAccess access, string kartRef, HhsWindow window, int page, int pageSize, CancellationToken ct) => throw new NotSupportedException();

    private HhsConsent Map(string rizaNo)
    {
        var s = Consents[rizaNo];
        return new HhsConsent(rizaNo, s.Drm, s.IptalKodu, $"http://fake-bank.test/gkd/{rizaNo}",
            s.Request.IzinTurleri, s.Request.ErisimIzniSonTrh, s.Request.HesapIslemBslZmn, s.Request.HesapIslemBtsZmn);
    }
}
