using FinCopilot.Application;
using FinCopilot.Infrastructure;
using Hangfire;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Hangfire işleri aynı SQL Server veritabanında, "HangFire" şemasında tutulur.
builder.Services.AddHangfire(cfg => cfg
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(builder.Configuration.GetConnectionString(FinCopilot.Infrastructure.DependencyInjection.ConnectionStringName)));
builder.Services.AddHangfireServer();

var host = builder.Build();
host.Run();
