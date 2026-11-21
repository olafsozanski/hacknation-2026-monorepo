using System.Globalization;
using System.Text.RegularExpressions;
using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Features.Search;

namespace ClerkAssistant.Api.Features.Intake;

public static partial class IntakeHeuristics
{
    public const int MaxSubjectLength = 200;
    public const int MaxItems = 20;
    public const int MaxItemLength = 500;

    private static readonly string[] MonthsGenitive =
    [
        "stycznia", "lutego", "marca", "kwietnia", "maja", "czerwca",
        "lipca", "sierpnia", "września", "października", "listopada", "grudnia",
    ];

    private static readonly HashSet<string> Abbreviations = new(StringComparer.OrdinalIgnoreCase)
    {
        "r", "ul", "al", "os", "pl", "art", "ust", "pkt", "nr", "poz", "tj", "np", "tzw", "dz", "u", "św", "im", "m", "lok",
        "godz", "zł", "ok", "tys", "mln", "ws",
    };

    public static RequestExtraction Analyze(
        string text, DateOnly today, IReadOnlyCollection<string> institutionalDomains, string? organizationAddress)
    {
        var lines = text.Split('\n').Select(l => l.Trim()).ToList();
        var items = RequestedItems(lines);
        var subject = Subject(text, lines, items);
        var isRequest = LooksLikePublicInformationRequest(text);
        return new RequestExtraction(
            subject,
            RequesterName(lines),
            RequesterContact(text, lines, institutionalDomains, organizationAddress),
            ReplyChannelOf(text),
            RequestDate(text, today),
            items.Count > 0 ? items.ToArray() : subject is null ? [] : [subject],
            isRequest,
            Remarks(text, isRequest));
    }

    public static bool LooksLikePublicInformationRequest(string text) => PublicInformationPhrase().IsMatch(text);

    public static string? CleanSubject(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var clean = SearchText.CollapseWhitespace(ListMarker().Replace(value.Trim(), ""))
            .Trim(' ', ',', ':', ';', '.', '-', '–', '—', '"', '„', '”', '\'', '?', '!', '(', ')');
        if (clean.Length < 3)
        {
            return null;
        }

        if (clean.Length > MaxSubjectLength)
        {
            var cut = clean.LastIndexOf(' ', MaxSubjectLength - 1);
            clean = clean[..(cut > MaxSubjectLength / 2 ? cut : MaxSubjectLength)].TrimEnd(' ', ',', ';', ':');
        }

        return char.ToUpper(clean[0], CultureInfo.GetCultureInfo("pl-PL")) + clean[1..];
    }

    public static DateOnly? ParseDate(string value)
    {
        var numeric = NumericDate().Match(value);
        if (numeric.Success)
        {
            return numeric.Groups["year4"].Success
                ? TryDate(numeric.Groups["year4"].Value, numeric.Groups["m2"].Value, numeric.Groups["d2"].Value)
                : TryDate(numeric.Groups["year"].Value, numeric.Groups["month"].Value, numeric.Groups["day"].Value);
        }

        var verbal = VerbalDate().Match(value);
        return verbal.Success
            ? TryDate(verbal.Groups["year"].Value, MonthNumber(verbal.Groups["month"].Value), verbal.Groups["day"].Value)
            : null;
    }

    private static string MonthNumber(string genitive)
    {
        var normalized = genitive.ToLowerInvariant().Replace('ś', 's').Replace('ź', 'z');
        var index = Array.FindIndex(MonthsGenitive, m => m.Replace('ś', 's').Replace('ź', 'z') == normalized);
        return (index + 1).ToString(CultureInfo.InvariantCulture);
    }

    private static string? Subject(string text, List<string> lines, List<string> items)
    {
        var labelled = SubjectLabel().Match(text);
        if (labelled.Success && CleanSubject(labelled.Groups["subject"].Value) is { } fromLabel)
        {
            return fromLabel;
        }

        foreach (Match phrase in RequestPhrase().Matches(text))
        {
            var sameLine = text[(phrase.Index + phrase.Length)..].TrimStart(' ', '\t');
            if (sameLine.Length == 0 || sameLine[0] == '\n')
            {
                continue;
            }

            var rest = LeadingConnector().Replace(sameLine, "");
            var sentence = FirstSentence(rest);
            if (sentence.Length >= 10 && !ListMarker().IsMatch(sentence))
            {
                return CleanSubject(sentence);
            }

            if (items.Count > 0)
            {
                return CleanSubject(items[0]);
            }
        }

        var line = lines.FirstOrDefault(IsMeaningfulLine);
        return line is null ? null : CleanSubject(FirstSentence(line));
    }

    private static List<string> RequestedItems(List<string> lines) => lines
        .Select(l => ListItem().Match(l))
        .Where(m => m.Success)
        .Select(m => m.Groups["item"].Value.Trim().TrimEnd(';', ','))
        .Where(item => item.Length >= 3)
        .Select(item => item.Length > MaxItemLength ? item[..MaxItemLength] : item)
        .Take(MaxItems)
        .ToList();

    private static string? RequesterName(List<string> lines)
    {
        foreach (var line in lines)
        {
            var labelled = NameLabel().Match(line);
            if (labelled.Success && labelled.Groups["name"].Value.Trim() is { Length: >= 3 and <= 120 } name)
            {
                return name;
            }
        }

        var closing = lines.FindLastIndex(l => ClosingPhrase().IsMatch(l));
        if (closing >= 0)
        {
            var signature = lines.Skip(closing + 1).Where(l => l.Length > 0).Take(3).FirstOrDefault(IsPersonName);
            if (signature is not null)
            {
                return signature;
            }
        }

        var header = lines.Where(l => l.Length > 0).Take(8).ToList();
        for (var i = 0; i < header.Count - 1; i++)
        {
            if (IsPersonName(header[i]) && LooksLikeContactLine(header[i + 1]))
            {
                return header[i];
            }
        }

        return null;
    }

    private static string? RequesterContact(
        string text, List<string> lines, IReadOnlyCollection<string> institutionalDomains, string? organizationAddress)
    {
        var email = EmailAddress().Matches(text)
            .Select(m => m.Value.TrimEnd('.'))
            .FirstOrDefault(e => !institutionalDomains.Any(d => e.EndsWith("@" + d, StringComparison.OrdinalIgnoreCase)
                                                               || e.EndsWith("." + d, StringComparison.OrdinalIgnoreCase)));
        if (email is not null)
        {
            return email;
        }

        for (var i = 0; i < lines.Count; i++)
        {
            var postal = PostalCodeLine().Match(lines[i]);
            if (!postal.Success
                || (organizationAddress?.Contains(postal.Groups["code"].Value, StringComparison.Ordinal) ?? false))
            {
                continue;
            }

            return i > 0 && StreetLine().IsMatch(lines[i - 1]) ? $"{lines[i - 1]}, {lines[i]}" : lines[i];
        }

        return null;
    }

    private static ReplyChannel? ReplyChannelOf(string text)
    {
        (Regex Pattern, ReplyChannel Channel)[] channels =
        [
            (EpuapChannel(), ReplyChannel.Epuap),
            (EmailChannel(), ReplyChannel.Email),
            (PostChannel(), ReplyChannel.Post),
            (InPersonChannel(), ReplyChannel.InPerson),
        ];
        return channels
            .Select(c => (c.Channel, Match: c.Pattern.Match(text)))
            .Where(c => c.Match.Success)
            .OrderBy(c => c.Match.Index)
            .Select(c => (ReplyChannel?)c.Channel)
            .FirstOrDefault();
    }

    private static DateOnly? RequestDate(string text, DateOnly today)
    {
        var earliest = today.AddYears(-2);
        var latest = today.AddDays(7);
        return AnyDate().Matches(text)
            .Select(m => ParseDate(m.Value))
            .FirstOrDefault(d => d is { } date && date >= earliest && date <= latest);
    }

    private static string? Remarks(string text, bool isRequest)
    {
        var remarks = new List<string>();
        if (PetitionPhrase().IsMatch(text))
        {
            remarks.Add("Pismo może być petycją — rozważ rozpatrzenie w trybie ustawy o petycjach.");
        }

        if (ComplaintPhrase().IsMatch(text))
        {
            remarks.Add("Pismo może zawierać skargę lub wniosek w trybie KPA — sprawdź właściwy tryb rozpatrzenia.");
        }

        if (ProcessedInformationPhrase().IsMatch(text))
        {
            remarks.Add("Wniosek może dotyczyć informacji przetworzonej — sprawdź, czy zachodzi szczególnie istotny interes publiczny.");
        }

        if (!isRequest)
        {
            remarks.Add("Nie znaleziono odwołania do dostępu do informacji publicznej — sprawdź, czy pismo jest wnioskiem w tym trybie.");
        }

        return remarks.Count == 0 ? null : string.Join(" ", remarks);
    }

    private static string FirstSentence(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '\n')
            {
                return text[..i];
            }

            if (c is '.' or '?' or '!' or ';' && (i + 1 == text.Length || char.IsWhiteSpace(text[i + 1]))
                && !(c == '.' && IsAbbreviationBefore(text, i)))
            {
                return text[..i];
            }
        }

        return text;
    }

    private static bool IsAbbreviationBefore(string text, int dotIndex)
    {
        var start = dotIndex;
        while (start > 0 && char.IsLetter(text[start - 1]))
        {
            start--;
        }

        return start < dotIndex && Abbreviations.Contains(text[start..dotIndex]);
    }

    private static bool IsMeaningfulLine(string line) =>
        line.Length >= 15
        && !line.Contains('@')
        && !NonSubjectLine().IsMatch(line)
        && !PostalCodeLine().IsMatch(line)
        && !(line.Length < 90 && GenericTitle().IsMatch(line));

    private static bool IsPersonName(string line) =>
        PersonName().IsMatch(line) && !InstitutionWord().IsMatch(line);

    private static bool LooksLikeContactLine(string line) =>
        StreetLine().IsMatch(line) || PostalCodeLine().IsMatch(line) || line.Contains('@');

    private static DateOnly? TryDate(string year, string month, string day) =>
        int.TryParse(year, out var y) && int.TryParse(month, out var m) && int.TryParse(day, out var d)
        && y is >= 1900 and <= 2100 && m is >= 1 and <= 12 && d >= 1 && d <= DateTime.DaysInMonth(y, m)
            ? new DateOnly(y, m, d)
            : null;

    [GeneratedRegex(@"^\s*(?:dotyczy|temat|w\s+sprawie)\s*:\s*(?<subject>[^\n]{3,})$", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex SubjectLabel();

    [GeneratedRegex(
        @"(?:udost[ęe]pnieni[ea]|przekazanie|przes[łl]anie|udzielenie|podanie)\s+(?:mi\s+|nam\s+)?(?:nast[ęe]puj[ąa]cych\s+|nast[ęe]puj[ąa]cej\s+)?informacji(?:\s+publicznej)?" +
        @"|(?:wnosz[ęe]|prosz[ęe]|wnioskuj[ęe]|zwracam\s+si[ęe]\s+z\s+(?:pro[śs]b[ąa]|wnioskiem))\s+o(?!\s+udost[ęe]pn)",
        RegexOptions.IgnoreCase)]
    private static partial Regex RequestPhrase();

    [GeneratedRegex(
        @"^(?:[\s,:\-–]*(?:w\s+zakresie|dotycz[ąa]c\p{L}*|na\s+temat|odno[śs]nie(?:\s+do)?|w\s+sprawie|w\s+przedmiocie|o\s+tre[śs]ci|tj\.|to\s+jest)\b)?[\s,:\-–]*",
        RegexOptions.IgnoreCase)]
    private static partial Regex LeadingConnector();

    [GeneratedRegex(@"^\s*(?:\d{1,2}[.)]|[a-z]\)|[-–•*])\s+")]
    private static partial Regex ListMarker();

    [GeneratedRegex(@"^\s*(?:\d{1,2}[.)]|[a-z]\)|[-–•*])\s+(?<item>.+)$")]
    private static partial Regex ListItem();

    [GeneratedRegex(@"^(?:wnioskodawca|imi[ęe]\s+i\s+nazwisko|nadawca|od)\s*:\s*(?<name>.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex NameLabel();

    [GeneratedRegex(
        @"^(?:z\s+powa[żz]aniem|z\s+wyrazami\s+szacunku|[łl][ąa]cz[ęe]\s+wyrazy\s+szacunku|(?:serdecznie\s+)?pozdrawiam|z\s+pozdrowieniami)\s*[,.!]?$",
        RegexOptions.IgnoreCase)]
    private static partial Regex ClosingPhrase();

    [GeneratedRegex(@"^\p{Lu}\p{Ll}+(?:[- ]\p{Lu}\p{Ll}+){1,3}$")]
    private static partial Regex PersonName();

    [GeneratedRegex(@"\b(?:Urz[ąa]d|Miasta|Prezydent|Biuro|Wydzia[łl]|Szanown\p{L}*|Dzie[ńn]|Gmina|Stowarzyszenie|Fundacja|Sp[óo][łl]ka)\b")]
    private static partial Regex InstitutionWord();

    [GeneratedRegex(@"[\w.+-]+@[\w-]+(?:\.[\w-]+)+")]
    private static partial Regex EmailAddress();

    [GeneratedRegex(@"(?<code>\d{2}-\d{3})\s+\p{Lu}")]
    private static partial Regex PostalCodeLine();

    [GeneratedRegex(@"^(?:ul\.|al\.|os\.|pl\.|ulica|aleja|osiedle|plac)\s*\S", RegexOptions.IgnoreCase)]
    private static partial Regex StreetLine();

    [GeneratedRegex(
        @"^(?:szanown\p{L}*|dzie[ńn]\s+dobry|witam|pan(?:i)?\s|urz[ąa]d|prezydent|biuro|wydzia[łl]|\p{L}+,\s+(?:dnia\s+)?\d|\d{1,2}[./-]\d{1,2}[./-]\d{2,4}|ul\.|al\.|tel|e-?mail|z\s+powa[żz]aniem|pozdrawiam)",
        RegexOptions.IgnoreCase)]
    private static partial Regex NonSubjectLine();

    [GeneratedRegex(@"wniosek\s+o\s+udost[ęe]pnienie\s+informacji|informacji\s+publicznej\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex GenericTitle();

    [GeneratedRegex(
        @"informacj\p{L}*\s+publiczn|dost[ęe]p\p{L}*\s+do\s+informacji|udost[ęe]pni\p{L}*\s+(?:mi\s+|nam\s+)?(?:nast[ęe]puj\p{L}+\s+)?informacj|art\.\s*61\s+konstytucji",
        RegexOptions.IgnoreCase)]
    private static partial Regex PublicInformationPhrase();

    [GeneratedRegex(@"\bepuap\b", RegexOptions.IgnoreCase)]
    private static partial Regex EpuapChannel();

    [GeneratedRegex(
        @"na\s+(?:m[óo]j\s+|wskazany\s+)?(?:adres\s+)?(?:e-?mail|mailowy|poczty\s+elektronicznej)|drog[ąa]\s+elektroniczn|poczt[ąa]\s+elektroniczn|mailowo|w\s+formie\s+elektronicznej",
        RegexOptions.IgnoreCase)]
    private static partial Regex EmailChannel();

    [GeneratedRegex(
        @"poczt[ąa]\s+tradycyjn|listownie|drog[ąa]\s+pocztow|listem\s+poleconym|w\s+formie\s+papierowej|na\s+adres\s+(?:do\s+)?(?:korespondencji|korespondencyjny|zamieszkania)",
        RegexOptions.IgnoreCase)]
    private static partial Regex PostChannel();

    [GeneratedRegex(@"odbi[óo]r\s+osobist|odbior[ęe]\s+osobi[śs]cie|osobi[śs]cie\s+w\s+urz[ęe]dzie", RegexOptions.IgnoreCase)]
    private static partial Regex InPersonChannel();

    [GeneratedRegex(
        @"(?<![\d.])(?:(?<year4>\d{4})-(?<m2>\d{2})-(?<d2>\d{2})|(?<day>\d{1,2})[./-](?<month>\d{1,2})[./-](?<year>\d{4}))(?![\d])")]
    private static partial Regex NumericDate();

    [GeneratedRegex(
        @"(?<day>\d{1,2})\s+(?<month>stycznia|lutego|marca|kwietnia|maja|czerwca|lipca|sierpnia|wrze[śs]nia|pa[źz]dziernika|listopada|grudnia)\s+(?<year>\d{4})",
        RegexOptions.IgnoreCase)]
    private static partial Regex VerbalDate();

    [GeneratedRegex(
        @"(?<![\d.])(?:\d{4}-\d{2}-\d{2}|\d{1,2}[./-]\d{1,2}[./-]\d{4})(?![\d])|\d{1,2}\s+(?:stycznia|lutego|marca|kwietnia|maja|czerwca|lipca|sierpnia|wrze[śs]nia|pa[źz]dziernika|listopada|grudnia)\s+\d{4}",
        RegexOptions.IgnoreCase)]
    private static partial Regex AnyDate();

    [GeneratedRegex(@"\bpetycj\p{L}*", RegexOptions.IgnoreCase)]
    private static partial Regex PetitionPhrase();

    [GeneratedRegex(@"\bskarg\p{L}*|\bskarż\p{L}*", RegexOptions.IgnoreCase)]
    private static partial Regex ComplaintPhrase();

    [GeneratedRegex(@"zestawie[nń]\p{L}*|\banaliz\p{L}*|wylicze[nń]\p{L}*|przetworzon\p{L}*|opracowani\p{L}*|zbiorcz\p{L}*", RegexOptions.IgnoreCase)]
    private static partial Regex ProcessedInformationPhrase();
}
