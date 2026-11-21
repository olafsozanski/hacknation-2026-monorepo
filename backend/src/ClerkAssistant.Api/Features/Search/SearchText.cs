using System.Text;

namespace ClerkAssistant.Api.Features.Search;

public static class SearchText
{
    public const int MaxQueryTokens = 25;
    private const int ShortQueryMaxLength = 160;
    private const int ShortQueryMaxWords = 12;
    private const int PrefixMatchMinLength = 6;

    private static readonly HashSet<string> Stopwords = new(StringComparer.Ordinal)
    {
        "albo", "bardzo", "będzie", "była", "było", "były", "będą", "czyli", "gdyby", "gdzie", "jako", "jakie",
        "jaki", "jaka", "jakim", "jednak", "jego", "jeśli", "jeżeli", "każdy", "kiedy", "która", "które",
        "którego", "której", "który", "których", "którym", "którzy", "mają", "może", "można", "mnie", "oraz",
        "przez", "przy", "również", "także", "takie", "tego", "tych", "tylko", "wraz", "więc", "wszystkie",
        "wszystkich", "żeby", "jest", "są", "dnia", "roku", "rok", "latach", "lata", "dzień", "dobry", "temu",
        "proszę", "prośbą", "uprzejmie", "zwracam", "wnoszę", "wnioskuję", "wniosek", "wniosku", "wnioskiem",
        "informacji", "informację", "informacja", "informacje", "publicznej", "publiczną", "publiczna",
        "dostępie", "dostępu", "udostępnienie", "udostępnienia", "udostępnić", "ustawy", "ustawa", "ustawą",
        "września", "2001", "szanowni", "szanowny", "szanowna", "państwo", "państwa", "pana", "pani", "panie",
        "poważaniem", "pozdrawiam", "pozdrowieniami", "elektronicznej", "elektroniczną", "drogą", "adres",
        "adresu", "mail", "email", "poczty", "pocztą", "dotyczy", "dotyczące", "dotyczącej", "dotyczących",
        "zakresie", "zakresu", "sprawie", "spraw", "sprawy", "związanych", "związku", "prowadzenie", "należy",
        "zadań", "podstawowych", "wydziału", "wydział", "biura", "biuro", "urzędu", "urząd", "urzędzie",
        "miasta", "miasto", "mieście", "bydgoszcz", "bydgoszczy", "następujących", "następujące", "przesłanie",
        "przekazanie", "odpowiedź", "odpowiedzi", "podstawie", "witam", "trybie", "stan", "stanu", "koniec", "końca",
        "według", "aktualne", "aktualny", "aktualnie", "obecnie", "podanie", "chcę", "pragnę", "zapytać", "zapytanie",
        "uprzejmą", "zwraca", "prosimy", "dziękuję", "styczeń", "luty", "marzec", "kwiecień", "maj", "czerwiec", "lipiec",
        "sierpień", "wrzesień", "październik", "listopad", "grudzień", "stycznia", "lutego", "marca", "kwietnia", "maja",
        "czerwca", "lipca", "sierpnia", "października", "listopada", "grudnia",
    };

    public static bool IsShortQuery(string text) =>
        text.Length <= ShortQueryMaxLength && Words(text).Count() <= ShortQueryMaxWords;

    public static IReadOnlyList<string> DistinctiveTokens(string text, int minLength = 4, int maxTokens = MaxQueryTokens)
    {
        var stats = new Dictionary<string, (int Count, int First)>(StringComparer.Ordinal);
        var position = 0;
        foreach (var word in Words(text))
        {
            position++;
            if (word.Length < minLength || Stopwords.Contains(word))
            {
                continue;
            }

            stats[word] = stats.TryGetValue(word, out var s) ? (s.Count + 1, s.First) : (1, position);
        }

        return stats
            .OrderByDescending(kv => kv.Value.Count)
            .ThenByDescending(kv => kv.Key.Length)
            .ThenBy(kv => kv.Value.First)
            .Take(maxTokens)
            .Select(kv => kv.Key)
            .ToList();
    }

    public static IReadOnlyList<(string Token, int Count)> QueryTokens(string text, int minLength, bool includeNumbers)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var order = new List<string>();
        foreach (var word in Words(text))
        {
            if (word.Length < minLength || Stopwords.Contains(word) || (!includeNumbers && word.All(char.IsDigit)))
            {
                continue;
            }

            if (counts.TryGetValue(word, out var count))
            {
                counts[word] = count + 1;
            }
            else
            {
                counts[word] = 1;
                order.Add(word);
            }
        }

        return order.Select(w => (w, counts[w])).ToList();
    }

    public static bool IsStemmable(string token) =>
        token.Length >= PrefixMatchMinLength && token.All(char.IsLetter);

    public static bool IsSafeToken(string token) => token.Length > 0 && token.All(char.IsLetterOrDigit);

    public static string? BuildOrQuery(IEnumerable<string> tokens)
    {
        var terms = tokens
            .Where(IsSafeToken)
            .Select(t => IsStemmable(t) ? $"{Stem(t)}:*" : t)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return terms.Count == 0 ? null : string.Join(" | ", terms);
    }

    public static string Stem(string token)
    {
        if (!IsStemmable(token))
        {
            return token;
        }

        var trimmed = token.Length >= 8 ? 3 : 2;
        return token[..Math.Max(5, token.Length - trimmed)];
    }


    public static IReadOnlyList<string> Stems(IEnumerable<string> tokens) =>
        tokens.Select(Stem).Distinct(StringComparer.Ordinal).ToList();

    public static string Snippet(string text, IReadOnlyList<string> stems, int maxLength = 240)
    {
        var plain = CollapseWhitespace(text);
        if (plain.Length <= maxLength)
        {
            return plain;
        }

        var match = FirstMatch(plain, stems);
        var start = match < 0 ? 0 : Math.Max(0, match - maxLength / 4);
        if (start > 0)
        {
            var space = plain.IndexOf(' ', start);
            start = space < 0 || space > match ? start : space + 1;
        }

        var length = Math.Min(maxLength, plain.Length - start);
        var end = start + length;
        if (end < plain.Length)
        {
            var space = plain.LastIndexOf(' ', end - 1, length);
            end = space > start + length / 2 ? space : end;
        }

        var snippet = plain[start..end].Trim();
        return (start > 0 ? "…" : "") + snippet + (end < plain.Length ? "…" : "");
    }

    public static string CollapseWhitespace(string text)
    {
        var builder = new StringBuilder(text.Length);
        var pendingSpace = false;
        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c) || char.IsControl(c))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(c);
        }

        return builder.ToString();
    }

    public static int FirstMatch(string text, IReadOnlyList<string> stems)
    {
        var best = -1;
        foreach (var stem in stems)
        {
            var index = IndexOfWordStart(text, stem);
            if (index >= 0 && (best < 0 || index < best))
            {
                best = index;
            }
        }

        return best;
    }

    private static int IndexOfWordStart(string text, string stem)
    {
        if (stem.Length == 0)
        {
            return -1;
        }

        var index = text.IndexOf(stem, StringComparison.OrdinalIgnoreCase);
        while (index > 0 && char.IsLetterOrDigit(text[index - 1]))
        {
            index = text.IndexOf(stem, index + 1, StringComparison.OrdinalIgnoreCase);
        }

        return index;
    }


    private static IEnumerable<string> Words(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormC).ToLowerInvariant();
        var word = new StringBuilder();
        foreach (var c in normalized)
        {
            if (char.IsLetterOrDigit(c))
            {
                word.Append(c);
                continue;
            }

            if (word.Length > 0)
            {
                yield return word.ToString();
                word.Clear();
            }
        }

        if (word.Length > 0)
        {
            yield return word.ToString();
        }
    }
}
