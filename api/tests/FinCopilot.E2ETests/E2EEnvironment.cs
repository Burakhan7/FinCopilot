using System.Net;
using System.Net.Sockets;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;
using FinCopilot.Infrastructure.Persistence;
using Hangfire;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;

namespace FinCopilot.E2ETests;

/// <summary>
/// Gerçek bileşenlerle ortam: SQL Server ve Mock HHS (mock-hhs/Dockerfile) container'da; Api gerçek Kestrel'de,
/// Hangfire sunucusu (Worker'ın işi) aynı süreçte. Mock HHS olayları host tüneliyle Api'nin /olay-dinleme ucuna gelir.
/// </summary>
public sealed class E2EEnvironment : IAsyncLifetime
{
    public const string YosKod = "8001";
    public const string RedirectUri = "fincopilot://gkd-donus";
    public const string OtpCode = "123456";

    private readonly int _apiPort = FreePort();
    private readonly int _mockPort = FreePort();
    private readonly MsSqlContainer _db = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
    private IFutureDockerImage? _mockImage;
    private IContainer? _mock;
    private ApiApp? _api;

    public Uri ApiBase => new($"http://localhost:{_apiPort}/");
    public Uri MockBase => new($"http://localhost:{_mockPort}/");

    public IServiceProvider Services => _api!.Services;

    public async Task InitializeAsync()
    {
        // Container'lar içinden Api'ye: host.testcontainers.internal:{apiPort}. Container'lar oluşturulmadan önce açılmalı.
        await TestcontainersSettings.ExposeHostPortsAsync((ushort)_apiPort);

        _mockImage = new ImageFromDockerfileBuilder()
            .WithDockerfileDirectory(Path.Combine(RepoRoot(), "mock-hhs"))
            .WithDockerfile("Dockerfile")
            .WithName("fincopilot-mock-hhs:e2e")
            .WithCleanUp(true)
            .Build();

        await Task.WhenAll(_db.StartAsync(), _mockImage.CreateAsync());

        _mock = new ContainerBuilder(_mockImage)
            .WithPortBinding(_mockPort, 8090)
            // Banka GKD adresi (hhsYonAdr) host'tan açılacak.
            .WithEnvironment("MOCK_HHS_PUBLIC_BASE_URL", MockBase.ToString().TrimEnd('/'))
            .WithEnvironment("MOCK_HHS_YOS_CODES", YosKod)
            .WithEnvironment("MOCK_HHS_YOS_EVENT_URL", $"http://host.testcontainers.internal:{_apiPort}/olay-dinleme")
            .WithEnvironment("MOCK_HHS_EVENT_TICK_SEC", "1")
            .WithEnvironment("MOCK_HHS_EVENT_RETRY_SCALE", "0.01")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(8090).ForPath("/_mock/health")))
            .Build();
        await _mock.StartAsync();

        // Api açılışta banka kataloğunu DB'ye yazar; şema önce hazır olmalı.
        await using (var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(_db.GetConnectionString()).Options))
            await db.Database.MigrateAsync();

        _api = new ApiApp(this, _db.GetConnectionString());
        _api.UseKestrel(_apiPort);
        _api.StartServer();

        // Worker açılışının yaptığı: tekrarlayan işler + abonelik kontrolü.
        Infrastructure.DependencyInjection.RegisterRecurringJobs(_api.Services);
    }

    public HttpClient CreateApiClient() => new() { BaseAddress = ApiBase };

    public HttpClient CreateMockClient(bool followRedirects = true) =>
        new(new HttpClientHandler { AllowAutoRedirect = followRedirects, UseCookies = true }) { BaseAddress = MockBase };

    public AppDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(_db.GetConnectionString()).Options);

    public async Task DisposeAsync()
    {
        if (_api is not null)
            await _api.DisposeAsync();
        if (_mock is not null)
            await _mock.DisposeAsync();
        await _db.DisposeAsync();
        if (_mockImage is not null)
            await _mockImage.DisposeAsync();
    }

    private sealed class ApiApp(E2EEnvironment env, string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:FinCopilot", connectionString);
            builder.UseSetting("Jwt:SigningKey", "uctan-uca-test-icin-en-az-otuz-iki-baytlik-anahtar");
            builder.UseSetting("Security:TcknHashKey", "uctan-uca-test-icin-tckn-hmac-anahtari-otuz-iki");
            builder.UseSetting("Auth:OtpFixedCode", OtpCode);
            builder.UseSetting("Auth:OtpResendCooldown", "00:00:00");
            builder.UseSetting("Hhs:YosKod", YosKod);
            builder.UseSetting("Hhs:RedirectUri", RedirectUri);

            string[][] banks = [["9001", "Anadolu Bank"], ["9002", "Ege Bank"], ["9003", "Marmara Bank"]];
            for (var i = 0; i < banks.Length; i++)
            {
                builder.UseSetting($"Hhs:Banks:{i}:HhsKod", banks[i][0]);
                builder.UseSetting($"Hhs:Banks:{i}:Name", banks[i][1]);
                builder.UseSetting($"Hhs:Banks:{i}:ApiBaseUrl", env.MockBase.ToString());
                builder.UseSetting($"Hhs:Banks:{i}:SupportsDecoupled", "true");
            }

            // Worker'ın yaptığı işi aynı süreçte yürüt.
            builder.ConfigureTestServices(s => s.AddHangfireServer(o =>
            {
                o.SchedulePollingInterval = TimeSpan.FromSeconds(1);
                o.WorkerCount = 4;
            }));
        }
    }

    private static int FreePort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (Directory.Exists(Path.Combine(dir.FullName, "mock-hhs")) && Directory.Exists(Path.Combine(dir.FullName, "api")))
                return dir.FullName;
        throw new DirectoryNotFoundException("Repo kökü (mock-hhs/ ve api/) bulunamadı.");
    }
}

[CollectionDefinition(Name)]
public sealed class E2ECollection : ICollectionFixture<E2EEnvironment>
{
    public const string Name = "e2e";
}
