using System.Text.RegularExpressions;
using ClerkAssistant.Api.Domain;

namespace ClerkAssistant.Api.Features.Anonymization;

public static partial class PersonalDataRules
{
    private const string PostalCity = @"(?<![\d-])\d{2}-\d{3}(?![\d-])[ ]+\p{Lu}[\p{L}-]*\p{L}";

    private const string Street =
        @"(?<![\p{L}.])(?:ul\.|al\.|os\.|pl\.|ulica|ulicy|aleja|alei|osiedle|osiedlu|plac|placu)[ ]?" +
        @"(?:[\p{Lu}\d][\p{L}\d'.-]*|\p{Ll}{1,5}\.)[ ](?:[\p{L}\d][\p{L}\d'.-]*[ ]){0,4}?" +
        @"\d{1,4}[a-zA-Z]?(?:[ ]?/[ ]?\d{1,4}[a-zA-Z]?|[ ](?:m\.|lok\.)[ ]?\d{1,4})?(?![\p{L}\d/])";

    private const string Date =
        @"\d{1,2}[./-]\d{1,2}[./-]\d{4}|\d{4}-\d{2}-\d{2}|" +
        @"\d{1,2}\s+(?i:stycznia|lutego|marca|kwietnia|maja|czerwca|lipca|sierpnia|września|października|listopada|grudnia)\s+\d{4}";

    private const string Place = @"\p{Lu}\p{Ll}+(?:[ -]\p{Lu}\p{Ll}+)?";

    private const string BirthContext = @"(?i:\bur\.|\burodzon\p{L}*|\bdata\s+(?:i\s+miejsce\s+)?urodzenia\s*:?)";

    private const string PersonName = @"\p{Lu}\p{Ll}+(?:-\p{Lu}\p{Ll}+)?(?:[ ]\p{Lu}\p{Ll}+(?:-\p{Lu}\p{Ll}+)?){1,2}";

    private const string SignatureName = PersonName + @"|\p{Lu}\.[ ]?\p{Lu}\p{Ll}+(?:-\p{Lu}\p{Ll}+)?";

    private static readonly int[] PeselWeights = [1, 3, 7, 9, 1, 3, 7, 9, 1, 3];
    private static readonly int[] NipWeights = [6, 5, 7, 2, 3, 4, 5, 6, 7];
    private static readonly int[] IdCardWeights = [7, 3, 1, 0, 7, 3, 1, 7, 3];

    private static readonly string[] Titles =
    [
        "prezydent", "wiceprezydent", "dyrektor", "zastępc", "burmistrz", "wojewod", "starost", "wójt", "marszał",
        "radn", "naczelnik", "kierownik", "prezes", "minister", "sekretarz", "skarbnik", "komendant", "przewodnicząc",
        "poseł", "posł", "senator", "rzecznik", "inspektor", "sędzi", "prokurator", "profesor", "doktor", "mecenas",
        "ksiądz", "księdz", "proboszcz", "biskup", "konserwator", "kurator", "notariusz", "komornik",
    ];

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
        AddMatches(found, content, PostalStreetPattern(), RedactionCategory.Address);
        AddMatches(found, content, PostalCityPattern(), RedactionCategory.Address);
        AddMatches(found, content, LandRegisterPattern(), RedactionCategory.PropertyRecord);
        AddGroups(found, content, ParcelPattern(), RedactionCategory.PropertyRecord);
        AddGroups(found, content, VehiclePlatePattern(), RedactionCategory.VehicleRegistration);
        AddBirthData(found, content);
        AddPersonNames(found, content);
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

    private static void AddGroups(List<Redaction> found, string content, Regex pattern, RedactionCategory category)
    {
        foreach (Match match in pattern.Matches(content))
        {
            var value = match.Groups["value"];
            found.Add(Suggest(content, value.Index, value.Length, category, RedactionSource.Rule));
        }
    }

    private static void AddBirthData(List<Redaction> found, string content)
    {
        foreach (var pattern in new[] { DateThenPlaceOfBirthPattern(), PlaceThenDateOfBirthPattern() })
        {
            foreach (Match match in pattern.Matches(content))
            {
                var first = match.Groups["first"];
                var last = match.Groups["last"].Success ? match.Groups["last"] : first;
                var end = last.Index + last.Length;
                found.Add(Suggest(content, first.Index, end - first.Index, RedactionCategory.DateOfBirth, RedactionSource.Rule));
            }
        }
    }

    private static void AddPersonNames(List<Redaction> found, string content)
    {
        var names = NamedPersonPatterns()
            .SelectMany(p => p.Matches(content))
            .Select(m => m.Groups["value"].Value)
            .Concat(SignatureNames(content))
            .Where(IsPrivatePersonName)
            .Distinct(StringComparer.Ordinal);
        foreach (var name in names)
        {
            found.AddRange(FindOccurrences(content, name, RedactionCategory.PersonName, RedactionSource.Rule));
        }
    }

    private static IEnumerable<Regex> NamedPersonPatterns() => [HonorificNamePattern(), LabelledNamePattern()];

    private static IEnumerable<string> SignatureNames(string content)
    {
        var lines = content.Split('\n').Select(l => l.Trim()).ToList();
        for (var i = 0; i < lines.Count; i++)
        {
            var closing = ClosingLine().Match(lines[i]);
            if (closing.Groups["value"].Success)
            {
                yield return closing.Groups["value"].Value;
            }
            else if (closing.Success)
            {
                var signature = lines.Skip(i + 1).Where(l => l.Length > 0).Take(3).FirstOrDefault(IsNameLine);
                if (signature is not null)
                {
                    yield return signature;
                }
            }
            else if (IsNameLine(lines[i]) && i + 1 < lines.Count && StreetLine().IsMatch(lines[i + 1]))
            {
                yield return lines[i];
            }
        }
    }

    private static bool IsNameLine(string line) => NameLine().IsMatch(line) && !InstitutionWord().IsMatch(line);

    private static bool IsPrivatePersonName(string name)
    {
        var first = name.Split(' ', '-')[0].ToLowerInvariant();
        return !Titles.Any(first.StartsWith) && !InstitutionWord().IsMatch(name);
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
        @"(?:\d{3}[ -]?\d{3}[ -]?\d{3}|\d{2}[ -]\d{3}[ -]\d{2}[ -]\d{2}|\d{2}[ -]\d{2}[ -]\d{2}[ -]\d{3}" +
        @"|\(\d{2}\)[ ]?\d{3}[ -]?\d{2}[ -]?\d{2}|\(\d{2}\)[ ]?\d{2}[ -]\d{2}[ -]\d{3})" +
        @"(?![ -]?\d|,\d|[ ]?(?:zł|PLN|złotych|tys\.))")]
    private static partial Regex PhonePattern();

    [GeneratedRegex(@"(?<![\w.+-])[\w.+-]+@[\w-]+(?:\.[\w-]+)+")]
    private static partial Regex EmailPattern();

    [GeneratedRegex(Street + @"(?:,?[ \t]*(?:\r?\n)?[ \t]*" + PostalCity + ")?")]
    private static partial Regex StreetAddressPattern();

    [GeneratedRegex(PostalCity + @",?[ \t]*(?:\r?\n)?[ \t]*" + Street)]
    private static partial Regex PostalStreetPattern();

    [GeneratedRegex(PostalCity)]
    private static partial Regex PostalCityPattern();

    [GeneratedRegex(@"(?<![\p{L}\d])[A-Z]{2}\d[A-Z][ ]?/[ ]?\d{8}[ ]?/[ ]?\d(?!\d)")]
    private static partial Regex LandRegisterPattern();

    [GeneratedRegex(
        @"(?i:\bdziałk\p{L}*(?:\s+(?:ewidencyjn|geodezyjn|gruntow)\p{L}*)?|\bdz\.(?:\s*ew\.)?)\s+(?i:o\s+)?(?i:nr|numer\p{L}*)\.?\s*" +
        @"(?<value>\d{1,5}(?:/\d{1,5})?(?:(?:\s*,\s*|\s+(?i:i|oraz)\s+)\d{1,5}(?:/\d{1,5})?)*)(?![\d/])")]
    private static partial Regex ParcelPattern();

    [GeneratedRegex(
        @"(?i:\bnr\.?\s*rej(?:estr\p{L}*|\.)?|\bnumer\p{L}*\s+rejestracyjn\p{L}*|\btablic\p{L}*(?:\s+rejestracyjn\p{L}*)?)" +
        @"\s*(?i:(?:nr|o\s+numerze)\.?\s*)?[:\-–]?\s*" +
        @"(?<value>[A-Z]{2,3}[ -]?(?=[0-9A-Z]{0,4}\d)[0-9A-Z]{4,5})(?![\p{L}\d])")]
    private static partial Regex VehiclePlatePattern();

    [GeneratedRegex(
        BirthContext + @"\s*(?i:(?:dnia|w\s+dniu)\s+)?(?<first>" + Date + @")(?:\s?r\.)?" +
        @"(?:,?\s*(?:w|we)\s+(?<last>" + Place + "))?")]
    private static partial Regex DateThenPlaceOfBirthPattern();

    [GeneratedRegex(
        @"(?i:\bur\.|\burodzon\p{L}*|\bmiejsce\s+urodzenia\s*:?)\s*(?:(?:w|we)\s+)?(?<first>" + Place + ")" +
        @"(?:,?\s*(?i:(?:dnia|w\s+dniu)\s+)?(?<last>" + Date + "))?")]
    private static partial Regex PlaceThenDateOfBirthPattern();

    [GeneratedRegex(@"(?<![\p{L}])(?i:pan|pani|panu|panem|panią|pana)[ ]+(?<value>" + PersonName + @")(?![\p{L}])")]
    private static partial Regex HonorificNamePattern();

    [GeneratedRegex(
        @"(?i:imi[ęe]\s+i\s+nazwisko|wnioskodawc\p{L}*|nadawc\p{L}*|właściciel\p{L}*|najemc\p{L}*|dłużni\p{L}*)" +
        @"[ \t]*[:\-–][ \t]*(?<value>" + PersonName + @")(?![\p{L}])")]
    private static partial Regex LabelledNamePattern();

    [GeneratedRegex(
        @"^(?i:z\s+poważaniem|z\s+wyrazami\s+szacunku|łączę\s+wyrazy\s+szacunku|(?:serdecznie\s+)?pozdrawiam|z\s+pozdrowieniami)" +
        @"\s*[,.!]?(?:\s*[|–—-]?\s*(?<value>" + SignatureName + @"))?\s*$")]
    private static partial Regex ClosingLine();

    [GeneratedRegex("^(?:" + SignatureName + ")$")]
    private static partial Regex NameLine();

    [GeneratedRegex(@"^(?:ul\.|al\.|os\.|pl\.|ulica|aleja|osiedle|plac)\s*\S", RegexOptions.IgnoreCase)]
    private static partial Regex StreetLine();

    [GeneratedRegex(
        @"\b(?:Urz[ąę]d\p{L}*|Miasta|Miasto|Prezydent\p{L}*|Biur\p{L}*|Wydzia\p{L}*|Szanown\p{L}*|Dzie[ńn]|Gmin\p{L}*|" +
        @"Stowarzyszeni\p{L}*|Fundacj\p{L}*|Spółk\p{L}*|Rad\p{L}{0,2}|Zarząd\p{L}*|Komend\p{L}*|Sąd\p{L}*|Prokuratur\p{L}*|" +
        @"Szkoł\p{L}*|Schronisk\p{L}*|Parafi\p{L}*|Komunikacji|Społecznej|Województw\p{L}*|Delegatur\p{L}*|Bydgoszcz\p{L}*)\b")]
    private static partial Regex InstitutionWord();
}
