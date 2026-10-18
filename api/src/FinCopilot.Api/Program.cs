using FinCopilot.Api.Infrastructure;
using FinCopilot.Application;
using FinCopilot.Application.Auth;
using FinCopilot.Infrastructure;
using FinCopilot.Infrastructure.Auth;
using FinCopilot.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Sabit SMS kodu sadece geliştirme ve testte kabul edilir.
var fixedOtp = builder.Configuration[$"{AuthOptions.SectionName}:{nameof(AuthOptions.OtpFixedCode)}"];
if (!string.IsNullOrEmpty(fixedOtp) && !builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Testing"))
    throw new InvalidOperationException("Auth:OtpFixedCode sadece Development/Testing ortamında kullanılabilir.");

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddJwtAuthentication(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
builder.Services.AddExceptionHandler<AppProblemExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database");

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

public partial class Program;
