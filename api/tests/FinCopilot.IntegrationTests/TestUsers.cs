using System.Net.Http.Headers;
using System.Net.Http.Json;
using FinCopilot.Application.Auth;

namespace FinCopilot.IntegrationTests;

internal static class TestUsers
{
    public static string NewPhone() => "05" + Random.Shared.NextInt64(100_000_000, 999_999_999);

    /// <summary>Checksum'ı geçerli, rastgele T.C. kimlik numarası.</summary>
    public static string NewTckn()
    {
        var d = new int[11];
        d[0] = Random.Shared.Next(1, 10);
        for (var i = 1; i < 9; i++)
            d[i] = Random.Shared.Next(0, 10);
        d[9] = (((d[0] + d[2] + d[4] + d[6] + d[8]) * 7 - (d[1] + d[3] + d[5] + d[7])) % 10 + 10) % 10;
        d[10] = d[..10].Sum() % 10;
        return string.Concat(d);
    }

    /// <summary>Kayıt olur ve JWT'yi istemcinin varsayılan başlığına koyar.</summary>
    public static async Task<HttpClient> RegisteredClientAsync(this ApiFactory factory)
    {
        var client = factory.CreateClient();
        var phone = NewPhone();

        var otp = await (await client.PostAsJsonAsync("/auth/otp", new { phone })).Content.ReadFromJsonAsync<RequestOtpResult>();
        var res = await client.PostAsJsonAsync("/auth/register", new
        {
            phone, challengeId = otp!.ChallengeId, code = ApiFactory.OtpCode,
            fullName = "Test Kullanıcı", pin = "482915", deviceId = "cihaz", platform = "android",
        });
        res.EnsureSuccessStatusCode();
        var tokens = await res.Content.ReadFromJsonAsync<AuthTokens>();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
        return client;
    }

    public static async Task<HttpClient> RegisteredClientWithTcknAsync(this ApiFactory factory, string? tckn = null)
    {
        var client = await factory.RegisteredClientAsync();
        (await client.PutAsJsonAsync("/me/tckn", new { tckn = tckn ?? NewTckn() })).EnsureSuccessStatusCode();
        return client;
    }
}
