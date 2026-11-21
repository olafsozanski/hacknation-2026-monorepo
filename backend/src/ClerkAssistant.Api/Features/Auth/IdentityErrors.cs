using Microsoft.AspNetCore.Identity;

namespace ClerkAssistant.Api.Features.Auth;

public static class IdentityErrors
{
    public static Dictionary<string, string[]> ToValidationErrors(IdentityResult result, string field) =>
        new() { [field] = result.Errors.Select(e => Translate(e.Code, e.Description)).ToArray() };

    private static string Translate(string code, string fallback) => code switch
    {
        "PasswordMismatch" => "Obecne hasło jest nieprawidłowe.",
        "PasswordTooShort" => "Hasło musi mieć co najmniej 12 znaków.",
        "PasswordRequiresDigit" => "Hasło musi zawierać cyfrę.",
        "PasswordRequiresLower" => "Hasło musi zawierać małą literę.",
        "PasswordRequiresUpper" => "Hasło musi zawierać wielką literę.",
        "DuplicateUserName" => "Użytkownik o takiej nazwie już istnieje.",
        "InvalidUserName" => "Nazwa użytkownika może zawierać tylko litery, cyfry oraz znaki . - _ @",
        _ => fallback,
    };
}
