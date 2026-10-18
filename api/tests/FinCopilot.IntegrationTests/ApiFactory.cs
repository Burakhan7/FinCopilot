using FinCopilot.Application.Abstractions;
using FinCopilot.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;

namespace FinCopilot.IntegrationTests;

/// <summary>Tüm testler tek bir SQL Server 2022 container'ını paylaşır. Docker Desktop açık olmalı.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string TestBankCode = "9001";
    public const string OtpCode = "123456";

    private readonly MsSqlContainer _db = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    /// <summary>Gerçek HHS yerine; testler bankadaki onay/iptal adımlarını bununla canlandırır.</summary>
    public FakeHhsClient Hhs { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:FinCopilot", _db.GetConnectionString());
        builder.UseSetting("Jwt:SigningKey", "entegrasyon-testi-icin-en-az-otuz-iki-baytlik-anahtar");
        builder.UseSetting("Security:TcknHashKey", "entegrasyon-testi-icin-tckn-hmac-anahtari-32b");
        builder.UseSetting("Auth:OtpFixedCode", OtpCode);
        builder.UseSetting("Auth:OtpResendCooldown", "00:00:00");

        string[][] banks = [["9001", "Anadolu Bank"], ["9002", "Ege Bank"], ["9003", "Marmara Bank"]];
        for (var i = 0; i < banks.Length; i++)
        {
            builder.UseSetting($"Hhs:Banks:{i}:HhsKod", banks[i][0]);
            builder.UseSetting($"Hhs:Banks:{i}:Name", banks[i][1]);
            builder.UseSetting($"Hhs:Banks:{i}:ApiBaseUrl", "http://fake-bank.test");
        }

        builder.ConfigureTestServices(s => s.AddSingleton<IHhsClient>(Hhs));
    }

    public async Task InitializeAsync()
    {
        await _db.StartAsync();

        // Api açılışta banka kataloğunu DB'ye yazar; bu yüzden şema host'tan önce hazır olmalı.
        await using (var db = CreateDbContext())
            await db.Database.MigrateAsync();
        _ = Services;
    }

    public AppDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(_db.GetConnectionString()).Options);

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _db.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
