using FinCopilot.Application.Abstractions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace FinCopilot.Api.Infrastructure;

/// <summary>Bankadan (HHS) gelen ve iş akışında karşılanmayan hatalar: 502, ÖHVPS hata kodu ile.</summary>
internal sealed class HhsExceptionHandler(IProblemDetailsService problemDetails, ILogger<HhsExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not HhsException hhs)
            return false;

        logger.LogWarning("HHS hatası {Status} {ErrorCode}: {Message}", hhs.StatusCode, hhs.ErrorCode, hhs.Message);

        httpContext.Response.StatusCode = StatusCodes.Status502BadGateway;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status502BadGateway,
                Title = "Bankaya ulaşırken bir sorun oluştu.",
                Detail = hhs.Message,
                Extensions = { ["code"] = "bank_error", ["bankErrorCode"] = hhs.ErrorCode },
            },
        });
    }
}
