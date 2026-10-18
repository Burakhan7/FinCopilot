using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FinCopilot.Application.Auth;
using FinCopilot.Application.Users;

namespace FinCopilot.IntegrationTests;

[Collection(ApiCollection.Name)]
public class AuthFlowTests(ApiFactory factory)
{
    private const string Pin = "482915";
    private readonly HttpClient _client = factory.CreateClient();

    private static string NewPhone() => "05" + Random.Shared.NextInt64(100_000_000, 999_999_999);

    private async Task<Guid> RequestOtpAsync(string phone)
    {
        var res = await _client.PostAsJsonAsync("/auth/otp", new { phone });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<RequestOtpResult>())!.ChallengeId;
    }

    private async Task<AuthTokens> RegisterAsync(string phone, string deviceId = "cihaz-1")
    {
        var challengeId = await RequestOtpAsync(phone);
        var res = await _client.PostAsJsonAsync("/auth/register", new
        {
            phone, challengeId, code = ApiFactory.OtpCode, fullName = "Ayşe Yılmaz", pin = Pin, deviceId, platform = "android",
        });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<AuthTokens>())!;
    }

    private Task<HttpResponseMessage> LoginAsync(string phone, string pin, string deviceId, Guid? challengeId = null, string? code = null) =>
        _client.PostAsJsonAsync("/auth/login", new { phone, pin, deviceId, platform = "android", challengeId, code });

    private Task<HttpResponseMessage> RefreshAsync(string refreshToken) =>
        _client.PostAsJsonAsync("/auth/refresh", new { refreshToken });

    private async Task<HttpResponseMessage> GetMeAsync(string accessToken)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "/me");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(req);
    }

    private static async Task<string?> ProblemCodeAsync(HttpResponseMessage res)
    {
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.TryGetProperty("code", out var c) ? c.GetString() : null;
    }

    [Fact]
    public async Task Register_then_me_returns_profile()
    {
        var phone = NewPhone();

        var tokens = await RegisterAsync(phone);
        var me = await (await GetMeAsync(tokens.AccessToken)).Content.ReadFromJsonAsync<MeDto>();

        Assert.Equal(PhoneNumber.Normalize(phone), me!.Phone);
        Assert.Equal("Ayşe Yılmaz", me.FullName);
        Assert.False(me.HasTckn);
    }

    [Fact]
    public async Task Me_requires_token()
    {
        var res = await _client.GetAsync("/me");

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Wrong_otp_is_rejected_and_challenge_dies_after_max_attempts()
    {
        var phone = NewPhone();
        var challengeId = await RequestOtpAsync(phone);
        object Body(string code) => new { phone, challengeId, code, fullName = "Ali Kaya", pin = Pin, deviceId = "d", platform = "ios" };

        for (var i = 0; i < 5; i++)
        {
            var wrong = await _client.PostAsJsonAsync("/auth/register", Body("000000"));
            Assert.Equal("otp_invalid", await ProblemCodeAsync(wrong));
        }

        // Deneme hakkı bitti: doğru kod da artık kabul edilmez.
        var right = await _client.PostAsJsonAsync("/auth/register", Body(ApiFactory.OtpCode));
        Assert.Equal(HttpStatusCode.BadRequest, right.StatusCode);
        Assert.Equal("otp_invalid", await ProblemCodeAsync(right));
    }

    [Fact]
    public async Task Otp_cannot_be_reused()
    {
        var phone = NewPhone();
        var challengeId = await RequestOtpAsync(phone);
        object body = new { phone, challengeId, code = ApiFactory.OtpCode, fullName = "Ali Kaya", pin = Pin, deviceId = "d", platform = "ios" };

        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsJsonAsync("/auth/register", body)).StatusCode);
        var again = await _client.PostAsJsonAsync("/auth/register", body);

        Assert.Equal("otp_invalid", await ProblemCodeAsync(again));
    }

    [Fact]
    public async Task Same_phone_cannot_register_twice()
    {
        var phone = NewPhone();
        await RegisterAsync(phone);

        var challengeId = await RequestOtpAsync(phone);
        var res = await _client.PostAsJsonAsync("/auth/register", new
        {
            phone, challengeId, code = ApiFactory.OtpCode, fullName = "Başka Biri", pin = Pin, deviceId = "d2", platform = "ios",
        });

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal("phone_already_registered", await ProblemCodeAsync(res));
    }

    [Fact]
    public async Task Weak_pin_is_rejected_with_validation_problem()
    {
        var phone = NewPhone();
        var challengeId = await RequestOtpAsync(phone);

        var res = await _client.PostAsJsonAsync("/auth/register", new
        {
            phone, challengeId, code = ApiFactory.OtpCode, fullName = "Ali Kaya", pin = "123456", deviceId = "d", platform = "ios",
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("Pin", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Otp_requests_are_rate_limited_per_phone()
    {
        var phone = NewPhone();
        for (var i = 0; i < 5; i++)
            await RequestOtpAsync(phone);

        var res = await _client.PostAsJsonAsync("/auth/otp", new { phone });

        Assert.Equal((HttpStatusCode)429, res.StatusCode);
        Assert.Equal("otp_rate_limited", await ProblemCodeAsync(res));
    }

    [Fact]
    public async Task Known_device_logs_in_with_pin_only()
    {
        var phone = NewPhone();
        await RegisterAsync(phone, "telefonum");

        var res = await LoginAsync(phone, Pin, "telefonum");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task New_device_needs_sms_then_becomes_known()
    {
        var phone = NewPhone();
        await RegisterAsync(phone, "eski-telefon");

        var withoutOtp = await LoginAsync(phone, Pin, "yeni-telefon");
        Assert.Equal(HttpStatusCode.Forbidden, withoutOtp.StatusCode);
        Assert.Equal("device_verification_required", await ProblemCodeAsync(withoutOtp));

        var challengeId = await RequestOtpAsync(phone);
        var withOtp = await LoginAsync(phone, Pin, "yeni-telefon", challengeId, ApiFactory.OtpCode);
        Assert.Equal(HttpStatusCode.OK, withOtp.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(phone, Pin, "yeni-telefon")).StatusCode);
    }

    [Fact]
    public async Task Unknown_phone_and_wrong_pin_give_same_error()
    {
        var phone = NewPhone();
        await RegisterAsync(phone);

        var wrongPin = await LoginAsync(phone, "999999", "cihaz-1");
        var unknown = await LoginAsync(NewPhone(), Pin, "cihaz-1");

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPin.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        Assert.Equal("invalid_credentials", await ProblemCodeAsync(wrongPin));
        Assert.Equal("invalid_credentials", await ProblemCodeAsync(unknown));
    }

    [Fact]
    public async Task Five_wrong_pins_lock_the_account()
    {
        var phone = NewPhone();
        await RegisterAsync(phone);

        for (var i = 0; i < 4; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(phone, "999999", "cihaz-1")).StatusCode);
        Assert.Equal((HttpStatusCode)423, (await LoginAsync(phone, "999999", "cihaz-1")).StatusCode);

        // Kilitliyken doğru PIN de reddedilir.
        var right = await LoginAsync(phone, Pin, "cihaz-1");
        Assert.Equal((HttpStatusCode)423, right.StatusCode);
        Assert.Equal("account_locked", await ProblemCodeAsync(right));
    }

    [Fact]
    public async Task Refresh_rotates_tokens()
    {
        var tokens = await RegisterAsync(NewPhone());

        var res = await RefreshAsync(tokens.RefreshToken);
        var rotated = await res.Content.ReadFromJsonAsync<AuthTokens>();

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.NotEqual(tokens.RefreshToken, rotated!.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, (await GetMeAsync(rotated.AccessToken)).StatusCode);
    }

    [Fact]
    public async Task Reusing_old_refresh_token_revokes_the_session()
    {
        var tokens = await RegisterAsync(NewPhone());
        var rotated = await (await RefreshAsync(tokens.RefreshToken)).Content.ReadFromJsonAsync<AuthTokens>();

        // Saldırgan eski token'ı kullanıyor.
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(tokens.RefreshToken)).StatusCode);

        // Meşru kullanıcının yeni token'ı da artık geçersiz.
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(rotated!.RefreshToken)).StatusCode);
    }

    [Fact]
    public async Task Logout_invalidates_refresh_token()
    {
        var tokens = await RegisterAsync(NewPhone());

        using var req = new HttpRequestMessage(HttpMethod.Post, "/auth/logout");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(req)).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(tokens.RefreshToken)).StatusCode);
    }

    [Fact]
    public async Task Relogin_on_same_device_ends_previous_session()
    {
        var phone = NewPhone();
        var first = await RegisterAsync(phone);

        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(phone, Pin, "cihaz-1")).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(first.RefreshToken)).StatusCode);
    }
}
