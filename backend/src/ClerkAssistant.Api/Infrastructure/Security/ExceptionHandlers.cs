using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace ClerkAssistant.Api.Infrastructure.Security;

public sealed class ConcurrencyExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is not DbUpdateConcurrencyException)
        {
            return false;
        }

        context.Response.StatusCode = StatusCodes.Status409Conflict;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails =
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Dane zostały w międzyczasie zmienione przez innego użytkownika.",
                Detail = "Odśwież stronę, aby zobaczyć aktualną wersję, i wprowadź zmiany ponownie.",
            },
        });
    }
}
