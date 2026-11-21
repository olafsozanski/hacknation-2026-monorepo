namespace ClerkAssistant.Api.Infrastructure.Common;

public sealed class ValidationErrors
{
    private readonly Dictionary<string, string[]> _errors = [];

    public bool Any => _errors.Count > 0;

    public void Add(string field, string message) =>
        _errors[field] = _errors.TryGetValue(field, out var existing) ? [.. existing, message] : [message];

    public void ThrowIfAny()
    {
        if (Any)
        {
            throw new ValidationException(_errors);
        }
    }
}
