using FinCopilot.Application;
using FinCopilot.Infrastructure;
using Hangfire;

var builder = Host.CreateApplicationBuilder(args);

// Hangfire deposu AddInfrastructure içinde; Worker ayrıca işleri yürüten sunucuyu çalıştırır.
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHangfireServer();

var host = builder.Build();

FinCopilot.Infrastructure.DependencyInjection.RegisterRecurringJobs(host.Services);

host.Run();
