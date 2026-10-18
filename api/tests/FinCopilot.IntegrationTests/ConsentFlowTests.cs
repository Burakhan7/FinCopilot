using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FinCopilot.Application.Abstractions;
using FinCopilot.Application.Banks;
using FinCopilot.Application.Consents;
using FinCopilot.Application.Users;
using FinCopilot.Domain.BankData;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FinCopilot.IntegrationTests;

[Collection(ApiCollection.Name)]
public class ConsentFlowTests(ApiFactory factory)
{
    private sealed record Started(Guid ConsentId, string RizaNo, string DrmKod);

    private async Task<Started> StartAsync(HttpClient client, string hhsKod = ApiFactory.TestBankCode)
    {
        var res = await client.PostAsJsonAsync("/consents", new { hhsKod });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var body = (await res.Content.ReadFromJsonAsync<CreateConsentResult>())!;

        var uri = new Uri(body.GkdUrl);
        var rizaNo = uri.AbsolutePath.Split('/').Last();
        var drmKod = QueryHelpers.ParseQuery(uri.Query)["drmKod"].ToString();
        Assert.False(string.IsNullOrEmpty(drmKod));
        return new Started(body.ConsentId, rizaNo, drmKod);
    }

    private static Task<HttpResponseMessage> CallbackAsync(HttpClient client, Started s, string? yetKod, string? drmKod = null) =>
        client.PostAsJsonAsync($"/consents/{s.ConsentId}/callback", new { rizaNo = s.RizaNo, drmKod = drmKod ?? s.DrmKod, yetKod });

    private async Task<(HttpClient Client, Started Consent)> ConnectedAsync()
    {
        var client = await factory.RegisteredClientWithTcknAsync();
        var s = await StartAsync(client);
        var res = await CallbackAsync(client, s, factory.Hhs.Authorize(s.RizaNo));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (client, s);
    }

    private static async Task<string?> ProblemCodeAsync(HttpResponseMessage res)
    {
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.TryGetProperty("code", out var c) ? c.GetString() : null;
    }

    private static async Task<ConsentDto> GetAsync(HttpClient client, Guid id) =>
        (await client.GetFromJsonAsync<ConsentDto>($"/consents/{id}"))!;

    [Fact]
    public async Task Banks_come_from_configuration()
    {
        var client = await factory.RegisteredClientAsync();

        var banks = await client.GetFromJsonAsync<List<BankDto>>("/banks");

        Assert.Equal(["9001", "9002", "9003"], banks!.Select(b => b.HhsKod).Order());
    }

    [Fact]
    public async Task Tckn_is_validated_set_once_and_unique()
    {
        var client = await factory.RegisteredClientAsync();
        var tckn = TestUsers.NewTckn();

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/me/tckn", new { tckn = "12345678901" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync("/me/tckn", new { tckn })).StatusCode);
        Assert.True((await client.GetFromJsonAsync<MeDto>("/me"))!.HasTckn);

        var again = await client.PutAsJsonAsync("/me/tckn", new { tckn = TestUsers.NewTckn() });
        Assert.Equal("tckn_already_set", await ProblemCodeAsync(again));

        var other = await factory.RegisteredClientAsync();
        var taken = await other.PutAsJsonAsync("/me/tckn", new { tckn });
        Assert.Equal(HttpStatusCode.Conflict, taken.StatusCode);
        Assert.Equal("tckn_in_use", await ProblemCodeAsync(taken));
    }

    [Fact]
    public async Task Tckn_is_stored_encrypted_and_sent_to_bank()
    {
        var tckn = TestUsers.NewTckn();
        var client = await factory.RegisteredClientWithTcknAsync(tckn);

        var s = await StartAsync(client);

        Assert.Equal(tckn, factory.Hhs.Consents[s.RizaNo].Request.Tckn);
        await using var db = factory.CreateDbContext();
        var consent = await db.Consents.SingleAsync(c => c.Id == s.ConsentId);
        var user = await db.Users.SingleAsync(u => u.Id == consent.UserId);
        Assert.DoesNotContain(tckn, System.Text.Encoding.UTF8.GetString(user.TcknEnc!));
        Assert.NotEqual(tckn, user.TcknHash);
    }

    [Fact]
    public async Task Connecting_a_bank_requires_tckn()
    {
        var client = await factory.RegisteredClientAsync();

        var res = await client.PostAsJsonAsync("/consents", new { hhsKod = ApiFactory.TestBankCode });

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal("tckn_required", await ProblemCodeAsync(res));
    }

    [Fact]
    public async Task Unknown_bank_is_rejected()
    {
        var client = await factory.RegisteredClientWithTcknAsync();

        var res = await client.PostAsJsonAsync("/consents", new { hhsKod = "1234" });

        Assert.Equal("bank_not_found", await ProblemCodeAsync(res));
    }

    [Fact]
    public async Task Consent_request_asks_for_all_permissions_for_six_months()
    {
        var client = await factory.RegisteredClientWithTcknAsync();

        var s = await StartAsync(client);

        var req = factory.Hhs.Consents[s.RizaNo].Request;
        Assert.Equal(["01", "02", "03", "04", "05", "06", "07", "08", "09"], req.IzinTurleri);
        Assert.InRange((req.ErisimIzniSonTrh - DateTimeOffset.UtcNow).TotalDays, 179, 180);
        Assert.InRange((DateTimeOffset.UtcNow - req.HesapIslemBslZmn).TotalDays, 364, 366);
        Assert.Equal("fincopilot://gkd-donus", req.YonAdr);
        Assert.Equal("B", (await GetAsync(client, s.ConsentId)).RizaDrm);
    }

    [Fact]
    public async Task Approved_gkd_stores_encrypted_tokens_and_connects()
    {
        var client = await factory.RegisteredClientWithTcknAsync();
        var s = await StartAsync(client);
        var yetKod = factory.Hhs.Authorize(s.RizaNo);

        var res = await CallbackAsync(client, s, yetKod);

        var dto = await res.Content.ReadFromJsonAsync<ConsentDto>();
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("K", dto!.RizaDrm);
        Assert.Equal("Bağlı", dto.Durum);
        Assert.Equal("Anadolu Bank", dto.BankName);

        await using var db = factory.CreateDbContext();
        var token = await db.ConsentTokens.SingleAsync(t => t.ConsentId == s.ConsentId);
        var protector = factory.Services.GetRequiredService<ISecretProtector>();
        var expected = factory.Hhs.Consents[s.RizaNo].AccessToken!;
        Assert.DoesNotContain(expected, System.Text.Encoding.UTF8.GetString(token.AccessTokenEnc));
        Assert.Equal(expected, protector.Unprotect(token.AccessTokenEnc, SecretPurpose.BankToken));
        Assert.Null((await db.Consents.SingleAsync(c => c.Id == s.ConsentId)).DrmKodHash);
    }

    [Fact]
    public async Task Wrong_drm_kod_is_rejected_and_real_one_still_works()
    {
        var client = await factory.RegisteredClientWithTcknAsync();
        var s = await StartAsync(client);
        var yetKod = factory.Hhs.Authorize(s.RizaNo);

        var forged = await CallbackAsync(client, s, yetKod, drmKod: "saldirgan");
        Assert.Equal(HttpStatusCode.BadRequest, forged.StatusCode);
        Assert.Equal("drm_kod_invalid", await ProblemCodeAsync(forged));

        Assert.Equal(HttpStatusCode.OK, (await CallbackAsync(client, s, yetKod)).StatusCode);
    }

    [Fact]
    public async Task Callback_cannot_be_replayed()
    {
        var client = await factory.RegisteredClientWithTcknAsync();
        var s = await StartAsync(client);
        var yetKod = factory.Hhs.Authorize(s.RizaNo);
        await CallbackAsync(client, s, yetKod);

        var replay = await CallbackAsync(client, s, yetKod);

        Assert.Equal("consent_not_pending", await ProblemCodeAsync(replay));
    }

    [Fact]
    public async Task User_cancelling_at_bank_is_reflected()
    {
        var client = await factory.RegisteredClientWithTcknAsync();
        var s = await StartAsync(client);
        factory.Hhs.CancelAtBank(s.RizaNo, "13");

        var dto = await (await CallbackAsync(client, s, yetKod: null)).Content.ReadFromJsonAsync<ConsentDto>();

        Assert.Equal("I", dto!.RizaDrm);
        Assert.Equal("13", dto.RizaIptDtyKod);
        Assert.Equal("Bankada vazgeçildi", dto.IptalNedeni);
    }

    [Fact]
    public async Task Connected_bank_cannot_be_connected_again()
    {
        var (client, _) = await ConnectedAsync();

        var res = await client.PostAsJsonAsync("/consents", new { hhsKod = ApiFactory.TestBankCode });

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal("consent_already_active", await ProblemCodeAsync(res));
    }

    [Fact]
    public async Task New_request_replaces_an_abandoned_one()
    {
        var client = await factory.RegisteredClientWithTcknAsync();
        var first = await StartAsync(client);

        var second = await StartAsync(client);

        var list = (await client.GetFromJsonAsync<List<ConsentDto>>("/consents"))!;
        Assert.Equal("I", list.Single(c => c.Id == first.ConsentId).RizaDrm);
        Assert.Equal("01", list.Single(c => c.Id == first.ConsentId).RizaIptDtyKod);
        Assert.Equal("B", list.Single(c => c.Id == second.ConsentId).RizaDrm);
    }

    [Fact]
    public async Task Other_banks_can_be_connected_in_parallel()
    {
        var (client, _) = await ConnectedAsync();

        var s = await StartAsync(client, "9002");

        Assert.Equal("B", (await GetAsync(client, s.ConsentId)).RizaDrm);
    }

    [Fact]
    public async Task Revoke_cancels_at_bank_drops_tokens_and_deactivates_accounts()
    {
        var (client, s) = await ConnectedAsync();
        var accessToken = factory.Hhs.Consents[s.RizaNo].AccessToken;
        await using (var db = factory.CreateDbContext())
        {
            var consent = await db.Consents.SingleAsync(c => c.Id == s.ConsentId);
            db.Accounts.Add(new Account { UserId = consent.UserId, ConsentId = consent.Id, HhsKod = consent.HhsKod, HspRef = "H1", ParaBirimi = "TRY" });
            await db.SaveChangesAsync();
        }

        var res = await client.DeleteAsync($"/consents/{s.ConsentId}");

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        Assert.Contains((s.RizaNo, accessToken), factory.Hhs.Revocations);
        var dto = await GetAsync(client, s.ConsentId);
        Assert.Equal("I", dto.RizaDrm);
        Assert.Equal("03", dto.RizaIptDtyKod);
        await using (var db = factory.CreateDbContext())
        {
            Assert.False(await db.ConsentTokens.AnyAsync(t => t.ConsentId == s.ConsentId));
            Assert.False((await db.Accounts.SingleAsync(a => a.ConsentId == s.ConsentId)).IsActive);
        }

        // Kaldırılan banka yeniden bağlanabilir.
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/consents", new { hhsKod = ApiFactory.TestBankCode })).StatusCode);
    }

    [Fact]
    public async Task Revocation_at_bank_is_picked_up()
    {
        var (client, s) = await ConnectedAsync();
        factory.Hhs.CancelAtBank(s.RizaNo, "02");

        var dto = await GetAsync(client, s.ConsentId);

        Assert.Equal("I", dto.RizaDrm);
        Assert.Equal("02", dto.RizaIptDtyKod);
        Assert.Equal("Banka üzerinden iptal edildi", dto.IptalNedeni);
    }

    [Fact]
    public async Task Revoking_after_bank_side_revocation_keeps_bank_reason()
    {
        var (client, s) = await ConnectedAsync();
        factory.Hhs.CancelAtBank(s.RizaNo, "02");

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/consents/{s.ConsentId}")).StatusCode);

        Assert.Equal("02", (await GetAsync(client, s.ConsentId)).RizaIptDtyKod);
    }

    [Fact]
    public async Task Consents_of_other_users_are_invisible()
    {
        var (_, s) = await ConnectedAsync();
        var stranger = await factory.RegisteredClientWithTcknAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/consents/{s.ConsentId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.DeleteAsync($"/consents/{s.ConsentId}")).StatusCode);
        Assert.DoesNotContain(await stranger.GetFromJsonAsync<List<ConsentDto>>("/consents") ?? [], c => c.Id == s.ConsentId);
    }

    [Fact]
    public async Task Consent_endpoints_require_login()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("/consents")).StatusCode);
    }
}
