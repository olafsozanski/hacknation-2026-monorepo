namespace ClerkAssistant.Api.Infrastructure.Common;

public static class ApiResults
{
    public static IResult ValidationError(string field, string message) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });

    public static IResult Conflict(string message) =>
        TypedResults.Problem(message, statusCode: StatusCodes.Status409Conflict);
}
