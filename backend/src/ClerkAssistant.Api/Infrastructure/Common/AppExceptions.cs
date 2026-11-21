namespace ClerkAssistant.Api.Infrastructure.Common;

public abstract class AppException(string message) : Exception(message);

public sealed class NotFoundException(string message = "Nie znaleziono zasobu.") : AppException(message);

public sealed class ConflictException(string message) : AppException(message);

public sealed class ForbiddenException(string message) : AppException(message);

public sealed class AuthenticationFailedException(string message, bool lockedOut = false) : AppException(message)
{
    public bool LockedOut { get; } = lockedOut;
}

public sealed class ValidationException(IReadOnlyDictionary<string, string[]> errors)
    : AppException("Sprawdź poprawność wprowadzonych danych.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;

    public static ValidationException For(string field, string message) => new(new Dictionary<string, string[]> { [field] = [message] });
}
