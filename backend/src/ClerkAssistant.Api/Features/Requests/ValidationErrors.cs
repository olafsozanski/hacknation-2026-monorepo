using Microsoft.AspNetCore.Http.HttpResults;

namespace ClerkAssistant.Api.Features.Requests;

public sealed class ValidationErrors
{
    private readonly Dictionary<string, string[]> _errors = [];

    public bool Any => _errors.Count > 0;

    public void Add(string field, string message) =>
        _errors[field] = _errors.TryGetValue(field, out var existing) ? [.. existing, message] : [message];

    public ValidationProblem ToProblem() => TypedResults.ValidationProblem(_errors);

    public static ValidationProblem Single(string field, string message)
    {
        var errors = new ValidationErrors();
        errors.Add(field, message);
        return errors.ToProblem();
    }
}
