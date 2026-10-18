using FinCopilot.Domain.Consents;
using FinCopilot.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;

namespace FinCopilot.IntegrationTests;

/// <summary>Tüm testler tek bir SQL Server 2022 container'ını paylaşır. Docker Desktop açık olmalı.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string TestBankCode = "9001";

    private readonly MsSqlContainer _db = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:FinCopilot", _db.GetConnectionString());
    }

    public async Task InitializeAsync()
    {
        await _db.StartAsync();

        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
        db.Banks.Add(new Bank { HhsKod = TestBankCode, Name = "Anadolu Bank", ApiBaseUrl = "http://localhost:8090" });
        await db.SaveChangesAsync();
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
