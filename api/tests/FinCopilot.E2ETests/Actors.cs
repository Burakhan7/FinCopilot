using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using FinCopilot.Application.Auth;
using FinCopilot.Application.Consents;

namespace FinCopilot.E2ETests;

/// <summary>Mobil uygulamanın backend'e yaptıkları.</summary>
internal sealed class AppUser(HttpClient api)
{
    public HttpClient Api { get; } = api;

    public static async Task<AppUser> RegisterAsync(E2EEnvironment env, string fullName, string tckn)
    {
        var api = env.CreateApiClient();
        var phone = "05" + Random.Shared.NextInt64(100_000_000, 999_999_999);
        var otp = await (await api.PostAsJsonAsync("/auth/otp", new { phone })).Content.ReadFromJsonAsync<RequestOtpResult>();
        var res = await api.PostAsJsonAsync("/auth/register", new
        {
            phone, challengeId = otp!.ChallengeId, code = E2EEnvironment.OtpCode, fullName, pin = "482915", deviceId = "e2e", platform = "android",
        });
        res.EnsureSuccessStatusCode();
        var tokens = (await res.Content.ReadFromJsonAsync<AuthTokens>())!;
        api.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        (await api.PutAsJsonAsync("/me/tckn", new { tckn })).EnsureSuccessStatusCode();
        return new AppUser(api);
    }

    public async Task<CreateConsentResult> StartConnectingAsync(string hhsKod, string gkdYontem = "Y")
    {
        var res = await Api.PostAsJsonAsync("/consents", new { hhsKod, gkdYontem });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<CreateConsentResult>())!;
    }

    /// <summary>Banka deep link ile döndü: parametreleri aynen backend'e ilet.</summary>
    public async Task<ConsentDto> ReturnFromBankAsync(Guid consentId, DeepLink link)
    {
        var res = await Api.PostAsJsonAsync($"/consents/{consentId}/callback", new { rizaNo = link["rizaNo"], drmKod = link["drmKod"], yetKod = link["yetKod"] });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<ConsentDto>())!;
    }

    public async Task<ConsentDto> ConsentAsync(Guid consentId) =>
        (await Api.GetFromJsonAsync<ConsentDto>($"/consents/{consentId}"))!;
}

/// <summary>Bankanın uygulamaya döndürdüğü adres: fincopilot://gkd-donus?rizaNo=..&amp;drmKod=..&amp;yetKod=..</summary>
internal sealed class DeepLink(Uri uri)
{
    private readonly Dictionary<string, string> _q = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query)
        .ToDictionary(p => p.Key, p => p.Value.ToString());

    public Uri Uri { get; } = uri;
    public string? this[string key] => _q.GetValueOrDefault(key);
}

/// <summary>Kullanıcının bankada yaptıkları (Mock HHS'nin HTML sayfaları). Şifre ve SMS kodu Mock HHS test değerleri.</summary>
internal sealed partial class BankUser(E2EEnvironment env, string tckn)
{
    private const string Password = "Test1234";
    private const string Sms = "123456";

    /// <summary>
    /// Yönlendirmeli GKD: banka sayfasında giriş → SMS → ürün seçimi → onay/vazgeç.
    /// Banka herhangi bir adımda uygulamaya döndürebilir (örn. TCKN uyuşmazlığı); dönüş adresini verir.
    /// </summary>
    public async Task<DeepLink> CompleteRedirectGkdAsync(string gkdUrl, bool approve = true, string? loginTckn = null)
    {
        using var web = env.CreateMockClient(followRedirects: false);
        var rizaNo = new Uri(gkdUrl).AbsolutePath.Split('/').Last();

        var page = await web.GetStringAsync(gkdUrl);
        var sid = Field(page, "sid");

        var step = await web.PostAsync($"gkd/{rizaNo}/giris", Form(("sid", sid), ("tckn", loginTckn ?? tckn), ("sifre", Password)));
        if (Redirected(step) is { } early1) return early1;

        step = await web.PostAsync($"gkd/{rizaNo}/otp", Form(("sid", sid), ("otp", Sms)));
        if (Redirected(step) is { } early2) return early2;
        var select = await step.Content.ReadAsStringAsync();

        var fields = new List<(string, string)> { ("sid", sid), ("islem", approve ? "onay" : "vazgec") };
        fields.AddRange(Products(select));
        step = await web.PostAsync($"gkd/{rizaNo}/onay", Form([.. fields]));
        return Redirected(step) ?? throw new InvalidOperationException($"Banka uygulamaya döndürmedi: {(int)step.StatusCode}");
    }

    /// <summary>Ayrık GKD: bankanın mobil uygulamasında giriş → bekleyen isteği onayla/reddet.</summary>
    public async Task DecideInBankAppAsync(string hhsKod, string rizaNo, bool approve = true)
    {
        using var web = env.CreateMockClient(followRedirects: true);
        var res = await web.PostAsync("banka-uygulamasi/giris", Form(("hhsKod", hhsKod), ("tckn", tckn), ("sifre", Password)));
        var page = await res.Content.ReadAsStringAsync();
        Assert.Contains(rizaNo, page);

        var fields = new List<(string, string)> { ("sid", Field(page, "sid")), ("rizaNo", rizaNo), ("islem", approve ? "onay" : "reddet") };
        fields.AddRange(Products(page).Distinct());
        (await web.PostAsync("banka-uygulamasi/onay", Form([.. fields]))).EnsureSuccessStatusCode();
    }

    private static DeepLink? Redirected(HttpResponseMessage res) =>
        res.StatusCode is HttpStatusCode.SeeOther or HttpStatusCode.Redirect && res.Headers.Location is { } loc ? new DeepLink(loc) : null;

    private static string Field(string html, string name) =>
        Regex.Match(html, $"name=\"{name}\" value=\"([^\"]+)\"") is { Success: true } m
            ? m.Groups[1].Value
            : throw new InvalidOperationException($"Sayfada '{name}' alanı yok.");

    private static IEnumerable<(string, string)> Products(string html) =>
        ProductInput().Matches(html).Select(m => (m.Groups[1].Value, m.Groups[2].Value));

    private static FormUrlEncodedContent Form(params (string Key, string Value)[] fields) =>
        new(fields.Select(f => new KeyValuePair<string, string>(f.Key, f.Value)));

    [GeneratedRegex("name=\"(hspRef|kartRef)\" value=\"([^\"]+)\"")]
    private static partial Regex ProductInput();
}

internal static class Eventually
{
    /// <summary>Arka plan işleri (Hangfire, olay iletimi) tamamlanana kadar bekler.</summary>
    public static async Task<T> GetAsync<T>(Func<Task<T>> probe, Func<T, bool> done, string what, int timeoutSeconds = 60)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        T last;
        do
        {
            last = await probe();
            if (done(last))
                return last;
            await Task.Delay(500);
        } while (DateTime.UtcNow < deadline);
        throw new TimeoutException($"{timeoutSeconds} sn içinde olmadı: {what} (son değer: {last})");
    }
}
