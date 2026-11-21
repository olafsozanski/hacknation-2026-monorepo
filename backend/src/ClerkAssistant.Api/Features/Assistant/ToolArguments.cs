using System.Globalization;

namespace ClerkAssistant.Api.Features.Assistant;

public readonly record struct ParsedArgument<T>(T? Value, bool Invalid) where T : struct;

public static class ToolArguments
{
    private static readonly string[] DateFormats = ["yyyy-MM-dd", "dd.MM.yyyy", "d.M.yyyy"];

    public static ParsedArgument<TEnum> ParseEnum<TEnum>(string? value) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new(null, false);
        }

        var text = value.Trim();
        return !char.IsDigit(text[0]) && Enum.TryParse<TEnum>(text, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
            ? new(parsed, false)
            : new(null, true);
    }

    public static ParsedArgument<DateOnly> ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new(null, false);
        }

        return DateOnly.TryParseExact(value.Trim(), DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? new(date, false)
            : new(null, true);
    }

    public static string CaseNotFound(string? caseNumber) =>
        $"Nie znaleziono sprawy o znaku {AssistantWording.Clip(caseNumber ?? "", 64)}. Sprawdź znak sprawy narzędziem find_requests.";
}
