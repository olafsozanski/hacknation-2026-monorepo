using Microsoft.AspNetCore.Antiforgery;

namespace ClerkAssistant.Api.Infrastructure.Security;

public sealed class AntiforgeryFilter(IAntiforgery antiforgery) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var method = context.HttpContext.Request.Method;
        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method))
        {
            return await next(context);
        }

        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            return TypedResults.Problem(
                "Sesja bezpieczeństwa wygasła. Odśwież stronę i spróbuj ponownie.",
                statusCode: StatusCodes.Status400BadRequest,
                type: "csrf");
        }

        return await next(context);
    }
}
