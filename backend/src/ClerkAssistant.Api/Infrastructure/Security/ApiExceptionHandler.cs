using ClerkAssistant.Api.Infrastructure.Common;
using ClerkAssistant.Api.Infrastructure.Documents;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClerkAssistant.Api.Infrastructure.Security;

public sealed class ApiExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        ProblemDetails? problem = exception switch
        {
            NotFoundException e => new ProblemDetails { Status = StatusCodes.Status404NotFound, Detail = e.Message },
            ConflictException e => new ProblemDetails { Status = StatusCodes.Status409Conflict, Detail = e.Message },
            AuthenticationFailedException e => new ProblemDetails
            {
                Status = e.LockedOut ? StatusCodes.Status423Locked : StatusCodes.Status401Unauthorized,
                Detail = e.Message,
            },
            ValidationException e => new HttpValidationProblemDetails(e.Errors.ToDictionary()) { Status = StatusCodes.Status400BadRequest, Detail = e.Message },
            DocumentRejectedException e => new HttpValidationProblemDetails(new Dictionary<string, string[]> { ["file"] = [e.Message] })
            {
                Status = StatusCodes.Status400BadRequest,
                Detail = e.Message,
            },
            DbUpdateConcurrencyException => new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Dane zostały w międzyczasie zmienione przez innego użytkownika.",
                Detail = "Odśwież stronę, aby zobaczyć aktualną wersję, i wprowadź zmiany ponownie.",
            },
            _ => null,
        };

        if (problem is null)
        {
            return false;
        }

        context.Response.StatusCode = problem.Status!.Value;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext { HttpContext = context, Exception = exception, ProblemDetails = problem });
    }
}
