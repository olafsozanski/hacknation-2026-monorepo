using ClerkAssistant.Api.Features.Search;

namespace ClerkAssistant.Api.Features.Assistant;

public sealed record GuideSection(string Title, IReadOnlyList<string> Routes, string Body)
{
    public string? Link => Routes.FirstOrDefault(r => !r.Contains(':'));
}

public sealed class AppGuide
{
    public const string ResourceName = "AppGuide.md";

    private const string SectionPrefix = "## ";
    private const string RoutePrefix = "Adres:";
    private const int MinWordLength = 4;
    private const int TitleWeight = 3;
    private const int BodyWeight = 1;
    private static readonly char[] RouteTrim = ['`', '.', ',', ';', '(', ')', '"', '„', '”'];

    private readonly IReadOnlyList<IndexedSection> _index;

    public AppGuide(string text)
    {
        Text = text.Replace("\r\n", "\n").Trim();
        Sections = Parse(Text);
        _index = Sections.Select(s => new IndexedSection(s, Words(s.Title), Words(s.Body))).ToList();
    }

    public string Text { get; }

    public IReadOnlyList<GuideSection> Sections { get; }

    public static AppGuide FromEmbeddedResource()
    {
        using var stream = typeof(AppGuide).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("The application guide is not embedded in the assembly.");
        using var reader = new StreamReader(stream);
        return new AppGuide(reader.ReadToEnd());
    }

    public static IReadOnlyList<GuideSection> Parse(string text)
    {
        var sections = new List<GuideSection>();
        string? title = null;
        IReadOnlyList<string> routes = [];
        var body = new List<string>();
        var routeAllowed = false;

        void Flush()
        {
            if (title is not null)
            {
                sections.Add(new GuideSection(title, routes, string.Join("\n", body).Trim()));
            }
        }

        foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.TrimEnd();
            if (line.StartsWith(SectionPrefix, StringComparison.Ordinal))
            {
                Flush();
                title = line[SectionPrefix.Length..].Trim();
                routes = [];
                body = [];
                routeAllowed = true;
                continue;
            }

            if (title is null || (routeAllowed && line.Length == 0))
            {
                continue;
            }

            if (routeAllowed && line.StartsWith(RoutePrefix, StringComparison.OrdinalIgnoreCase))
            {
                routes = ParseRoutes(line[RoutePrefix.Length..]);
                routeAllowed = false;
                continue;
            }

            routeAllowed = false;
            body.Add(line);
        }

        Flush();
        return sections;
    }

    public static bool Matches(string pattern, string path)
    {
        var expected = Segments(pattern);
        var actual = Segments(PathOnly(path));
        if (expected.Length != actual.Length)
        {
            return false;
        }

        return expected.Zip(actual).All(pair => pair.First.StartsWith(':')
            ? Guid.TryParse(pair.Second, out _)
            : string.Equals(pair.First, pair.Second, StringComparison.OrdinalIgnoreCase));
    }

    public GuideSection? PageFor(string path) =>
        Sections
            .SelectMany(s => s.Routes.Where(r => Matches(r, path)).Select(r => (Section: s, Parameters: r.Count(c => c == ':'))))
            .OrderBy(match => match.Parameters)
            .Select(match => match.Section)
            .FirstOrDefault();

    public IReadOnlyList<GuideSection> Rank(string question, int limit)
    {
        var stems = SearchText.Stems(SearchText.QueryTokens(question, MinWordLength, includeNumbers: false).Select(t => t.Token));
        if (stems.Count == 0)
        {
            return [];
        }

        return _index
            .Select(entry => (entry.Section, Score: stems.Sum(entry.Weight)))
            .Where(scored => scored.Score > 0)
            .OrderByDescending(scored => scored.Score)
            .Take(limit)
            .Select(scored => scored.Section)
            .ToList();
    }

    private static IReadOnlyList<string> ParseRoutes(string value) =>
        value.Split([' ', '\t', ','], StringSplitOptions.RemoveEmptyEntries)
            .Select(token => token.Trim(RouteTrim))
            .Where(token => token.StartsWith('/'))
            .ToList();

    private static string PathOnly(string path)
    {
        var end = path.IndexOfAny(['?', '#']);
        return end < 0 ? path : path[..end];
    }

    private static string[] Segments(string path) => path.Split('/', StringSplitOptions.RemoveEmptyEntries);

    private static string[] Words(string text) =>
        SearchText.QueryTokens(text, MinWordLength, includeNumbers: false).Select(t => t.Token).ToArray();

    private sealed record IndexedSection(GuideSection Section, string[] TitleWords, string[] BodyWords)
    {
        public int Weight(string stem) =>
            TitleWords.Any(w => w.StartsWith(stem, StringComparison.Ordinal)) ? TitleWeight
            : BodyWords.Any(w => w.StartsWith(stem, StringComparison.Ordinal)) ? BodyWeight
            : 0;
    }
}
