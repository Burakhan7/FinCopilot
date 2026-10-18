using FinCopilot.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace FinCopilot.Infrastructure.Auth;

/// <summary>Gerçek SMS sağlayıcısı seçilene kadar: mesajı sadece loga yazar.</summary>
internal sealed class LoggingSmsSender(ILogger<LoggingSmsSender> logger) : ISmsSender
{
    public Task SendAsync(string phoneE164, string message, CancellationToken cancellationToken)
    {
        logger.LogInformation("[SAHTE SMS] {Phone}: {Message}", phoneE164, message);
        return Task.CompletedTask;
    }
}
