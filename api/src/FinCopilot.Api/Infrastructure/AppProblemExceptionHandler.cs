using FinCopilot.Application.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace FinCopilot.Api.Infrastructure;

/// <summary>İş kuralı hatalarını ProblemDetails'e çevirir; istemci "code" alanına göre dallanır.</summary>
internal sealed class AppProblemExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not AppProblemException problem)
            return false;

        httpContext.Response.StatusCode = problem.StatusCode;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = problem.StatusCode,
                Title = problem.Title,
                Extensions = { ["code"] = problem.Code },
            },
        });
    }
}
