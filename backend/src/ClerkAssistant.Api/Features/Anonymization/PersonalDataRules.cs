using System.Text.RegularExpressions;
using ClerkAssistant.Api.Domain;

namespace ClerkAssistant.Api.Features.Anonymization;

public static partial class PersonalDataRules
{
    private const string PostalCity = @"(?<![\d-])\d{2}-\d{3}(?![\d-])[ ]+\p{Lu}[\p{L}-]*\p{L}";

    private const string Street =
        @"(?<![\p{L}.])(?:ul\.|al\.|os\.|pl\.|ulica|ulicy|aleja|alei|osiedle|osiedlu|plac|placu)[ ]?" +
        @"(?:(?:[\p{Lu}\d][\p{L}\d'.-]*|\p{Ll}{1,5}\.)[ ]){1,5}?" +
        @"\d{1,4}[a-zA-Z]?(?:[ ]?/[ ]?\d{1,4}[a-zA-Z]?|[ ](?:m\.|lok\.)[ ]?\d{1,4})?(?![\p{L}\d/])";

    private static readonly int[] PeselWeights = [1, 3, 7, 9, 1, 3, 7, 9, 1, 3];
    private static readonly int[] NipWeights = [6, 5, 7, 2, 3, 4, 5, 6, 7];
    private static readonly int[] IdCardWeights = [7, 3, 1, 0, 7, 3, 1, 7, 3];

    public static List<Redaction> Detect(string content)
    {
        var found = new List<Redaction>();
        AddMatches(found, content, PeselPattern(), RedactionCategory.Pesel, IsValidPesel);
        AddMatches(found, content, NipPattern(), RedactionCategory.Nip, IsValidNip);
        AddMatches(found, content, IdCardPattern(), RedactionCategory.IdDocument, IsValidIdCard);
        AddMatches(found, content, BankAccountPattern(), RedactionCategory.BankAccount, IsValidPolishIban);
        AddMatches(found, content, PhonePattern(), RedactionCategory.Phone);
        AddMatches(found, content, EmailPattern(), RedactionCategory.Email);
        AddMatches(found, content, StreetAddressPattern(), RedactionCategory.Address);
        AddMatches(found, content, PostalCityPattern(), RedactionCategory.Address);
        foreach (Match match in DateOfBirthPattern().Matches(content))
        {
            var date = match.Groups["date"];
            found.Add(Suggest(content, date.Index, date.Length, RedactionCategory.DateOfBirth, RedactionSource.Rule));
        }

        return RedactionSet.Merge(found);
    }

    public static IEnumerable<Redaction> FindOccurrences(
        string content, string value, RedactionCategory category, RedactionSource source)
    {
        if (value.Length < 2)
        {
            yield break;
        }

        var index = content.IndexOf(value, StringComparison.Ordinal);
        while (index >= 0)
        {
            if (IsWholeWord(content, index, value.Length))
            {
                yield return Suggest(content, index, value.Length, category, source);
            }

            index = content.IndexOf(value, index + value.Length, StringComparison.Ordinal);
        }
    }

    public static Redaction Suggest(
        string content, int start, int length, RedactionCategory category, RedactionSource source) =>
        new(start, length, content.Substring(start, length), category, RedactionStatus.Suggested, source);

    public static bool IsValidPesel(string value)
    {
        var digits = Digits(value);
        if (digits.Length != 11)
        {
            return false;
        }

        var sum = PeselWeights.Select((w, i) => w * digits[i]).Sum();
        return (10 - sum % 10) % 10 == digits[10];
    }

    public static bool IsValidNip(string value)
    {
        var digits = Digits(value);
        if (digits.Length != 10)
        {
            return false;
        }

        var check = NipWeights.Select((w, i) => w * digits[i]).Sum() % 11;
        return check != 10 && check == digits[9];
    }

    public static bool IsValidIdCard(string value)
    {
        var compact = value.Replace(" ", "");
        if (compact.Length != 9
            || !compact[..3].All(char.IsAsciiLetterUpper)
            || !compact[3..].All(char.IsAsciiDigit))
        {
            return false;
        }

        var sum = 0;
        for (var i = 0; i < compact.Length; i++)
        {
            var symbol = char.IsAsciiLetterUpper(compact[i]) ? compact[i] - 'A' + 10 : compact[i] - '0';
            sum += IdCardWeights[i] * symbol;
        }

        return sum % 10 == compact[3] - '0';
    }

    public static bool IsValidPolishIban(string value)
    {
        var compact = value.Replace(" ", "");
        if (compact.StartsWith("PL", StringComparison.OrdinalIgnoreCase))
        {
            compact = compact[2..];
        }

        if (compact.Length != 26 || !compact.All(char.IsAsciiDigit))
        {
            return false;
        }

        var rearranged = compact[2..] + "2521" + compact[..2];
        var remainder = 0;
        foreach (var c in rearranged)
        {
            remainder = (remainder * 10 + (c - '0')) % 97;
        }

        return remainder == 1;
    }

    private static void AddMatches(
        List<Redaction> found, string content, Regex pattern, RedactionCategory category, Func<string, bool>? isValid = null)
    {
        foreach (Match match in pattern.Matches(content))
        {
            if (isValid is null || isValid(match.Value))
            {
                found.Add(Suggest(content, match.Index, match.Length, category, RedactionSource.Rule));
            }
        }
    }

    private static bool IsWholeWord(string content, int start, int length) =>
        (start == 0 || !char.IsLetterOrDigit(content[start - 1]))
        && (start + length == content.Length || !char.IsLetterOrDigit(content[start + length]));

    private static int[] Digits(string value) => value.Where(char.IsAsciiDigit).Select(c => c - '0').ToArray();

    [GeneratedRegex(@"(?<![\d-])\d{11}(?![\d-])")]
    private static partial Regex PeselPattern();

    [GeneratedRegex(@"(?<![\d-])(?:\d{3}-\d{3}-\d{2}-\d{2}|\d{3}-\d{2}-\d{2}-\d{3}|\d{10})(?![\d-])")]
    private static partial Regex NipPattern();

    [GeneratedRegex(@"(?<![\p{L}\d])[A-Z]{3}[ ]?\d{6}(?![\p{L}\d])")]
    private static partial Regex IdCardPattern();

    [GeneratedRegex(@"(?<![\p{L}\d])(?:PL[ ]?)?\d{2}(?:[ ]?\d{4}){6}(?!\d)")]
    private static partial Regex BankAccountPattern();

    [GeneratedRegex(
        @"(?<![\d+])(?<!\d[ -])(?:(?:\+|00)48[ -]?|\(\+?48\)[ ]?)?" +
        @"(?:\d{3}[ -]?\d{3}[ -]?\d{3}|\d{2}[ -]\d{3}[ -]\d{2}[ -]\d{2}|\d{2}[ -]\d{2}[ -]\d{2}[ -]\d{3}|\(\d{2}\)[ ]?\d{3}[ -]?\d{2}[ -]?\d{2})" +
        @"(?![ -]?\d|,\d|[ ]?(?:zł|PLN|złotych|tys\.))")]
    private static partial Regex PhonePattern();

    [GeneratedRegex(@"(?<![\w.+-])[\w.+-]+@[\w-]+(?:\.[\w-]+)+")]
    private static partial Regex EmailPattern();

    [GeneratedRegex(Street + @"(?:,?[ \t]*(?:\r?\n)?[ \t]*" + PostalCity + ")?")]
    private static partial Regex StreetAddressPattern();

    [GeneratedRegex(PostalCity)]
    private static partial Regex PostalCityPattern();

    [GeneratedRegex(
        @"(?:\bur\.|\burodzon\p{L}*|\bdata\s+urodzenia:?)\s*(?:(?:dnia|w\s+dniu)\s+)?" +
        @"(?<date>\d{1,2}[./-]\d{1,2}[./-]\d{4}|\d{4}-\d{2}-\d{2}|\d{1,2}\s+(?:stycznia|lutego|marca|kwietnia|maja|czerwca|lipca|sierpnia|września|października|listopada|grudnia)\s+\d{4})",
        RegexOptions.IgnoreCase)]
    private static partial Regex DateOfBirthPattern();
}
