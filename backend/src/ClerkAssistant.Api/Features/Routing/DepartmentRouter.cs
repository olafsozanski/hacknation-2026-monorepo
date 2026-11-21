using System.Globalization;
using System.Text;
using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Features.Search;
using ClerkAssistant.Api.Infrastructure.Ai;
using ClerkAssistant.Api.Infrastructure.Common;
using ClerkAssistant.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ClerkAssistant.Api.Features.Routing;

public sealed record RoutingOutcome(DepartmentSuggestion[] Suggestions, bool AiUsed, bool AiFailed);

internal sealed record RoutingAnswer(List<RoutingChoice>? Departments);

internal sealed record RoutingChoice(string? Symbol, string? Reason, double Confidence);

public sealed class DepartmentRouter(
    AppDbContext db, HybridSearch search, AiGateway ai, IOptions<OrganizationOptions> organization, AppClock clock)
{
    public const int MaxSuggestions = 5;
    public const int MaxReasonLength = 200;
    private const string Config = AppDbContext.TextSearchConfig;
    private const int SimilarRequestsConsidered = 10;
    private const int MaxScopeInPrompt = 600;
    private const int MaxRequestInPrompt = 6000;
    private const int MaxScopeFragment = 160;

    public async Task<RoutingOutcome> SuggestAsync(
        string text, Guid? excludeRequestId, QueryEmbedding? embedding, CancellationToken ct)
    {
        var departments = await db.Departments.Where(d => d.IsActive).OrderBy(d => d.Symbol).ToListAsync(ct);
        if (departments.Count == 0 || string.IsNullOrWhiteSpace(text))
        {
            return new RoutingOutcome([], false, false);
        }

        var history = await search.SearchAsync(new SearchRequest(text)
        {
            IncludeKnowledge = false,
            ExcludeRequestId = excludeRequestId,
            PreferReceivedSince = clock.Today.AddMonths(-12),
            Limit = SimilarRequestsConsidered,
            Embedding = embedding,
        }, ct);
        var evidence = await GatherEvidenceAsync(text, departments, history.Hits, ct);
        var fallback = RankWithoutModel(evidence);

        var answer = await ai.GetStructuredAsync<RoutingAnswer>(
            "suggest-departments", SystemPrompt(), UserPrompt(text, departments, evidence), ct);
        var modelSuggestions = answer.Value is null ? [] : FromModel(answer.Value, departments, evidence);
        if (modelSuggestions.Length == 0)
        {
            return new RoutingOutcome(fallback, false, history.AiFailed || answer.Failed);
        }

        var merged = modelSuggestions
            .Concat(fallback.Where(f => modelSuggestions.All(m => m.DepartmentId != f.DepartmentId)))
            .Take(MaxSuggestions)
            .ToArray();
        return new RoutingOutcome(merged, true, history.AiFailed);
    }

    private async Task<Dictionary<Guid, Evidence>> GatherEvidenceAsync(
        string text, List<Department> departments, SearchHit[] similar, CancellationToken ct)
    {
        var bySymbol = departments.ToDictionary(d => d.Symbol, StringComparer.Ordinal);
        var caseCounts = similar
            .SelectMany(h => h.DepartmentSymbols.Distinct())
            .Where(bySymbol.ContainsKey)
            .GroupBy(s => bySymbol[s].Id)
            .ToDictionary(g => g.Key, g => g.Count());
        var casesWithDepartments = similar.Count(h => h.DepartmentSymbols.Any(bySymbol.ContainsKey));
        var averages = await AverageResponseDaysAsync(ct);
        var tokens = SearchText.DistinctiveTokens(text);
        var keywordRanks = await KeywordRanksAsync(tokens, ct);
        var maxRank = keywordRanks.Count == 0 ? 1 : keywordRanks.Values.Max();
        var stems = SearchText.Stems(tokens);

        return departments.ToDictionary(d => d.Id, d => new Evidence(
            d,
            caseCounts.GetValueOrDefault(d.Id),
            casesWithDepartments,
            averages.TryGetValue(d.Id, out var average) ? average : null,
            keywordRanks.TryGetValue(d.Id, out var rank) ? rank / maxRank : 0,
            keywordRanks.ContainsKey(d.Id) ? ScopeFragment(d.Scope, stems) : null));
    }

    private async Task<Dictionary<Guid, double>> KeywordRanksAsync(IReadOnlyList<string> tokens, CancellationToken ct)
    {
        var tokenQuery = SearchText.BuildOrQuery(tokens);
        if (tokenQuery is null)
        {
            return [];
        }

        var ranks = await db.Departments
            .Where(d => d.IsActive)
            .Select(d => new
            {
                d.Id,
                Rank = EF.Functions.ToTsVector(Config, d.Name + " " + d.Scope)
                    .RankCoverDensity(
                        EF.Functions.ToTsQuery(Config, tokenQuery),
                        NpgsqlTsRankingNormalization.DivideBy1PlusLogLength),
            })
            .Where(x => x.Rank > 0)
            .OrderByDescending(x => x.Rank)
            .Take(MaxSuggestions)
            .ToListAsync(ct);
        return ranks.ToDictionary(r => r.Id, r => (double)r.Rank);
    }

    private async Task<Dictionary<Guid, double>> AverageResponseDaysAsync(CancellationToken ct)
    {
        var answered = await db.RequestDepartments
            .Where(rd => rd.AskedOn != null && rd.AnsweredOn != null && rd.AnsweredOn >= rd.AskedOn)
            .Select(rd => new { rd.DepartmentId, AskedOn = rd.AskedOn!.Value, AnsweredOn = rd.AnsweredOn!.Value })
            .ToListAsync(ct);
        return answered
            .GroupBy(a => a.DepartmentId)
            .ToDictionary(
                g => g.Key,
                g => Math.Round(g.Average(a => a.AnsweredOn.DayNumber - a.AskedOn.DayNumber), 1));
    }

    private static DepartmentSuggestion[] RankWithoutModel(Dictionary<Guid, Evidence> evidence) => evidence.Values
        .Where(e => e.SimilarCases > 0 || e.KeywordScore > 0)
        .Select(e => Suggestion(
            e,
            FallbackReason(e),
            e.Confidence,
            e.SimilarCases > 0 ? SuggestionSource.History : SuggestionSource.Keywords))
        .OrderByDescending(s => s.Confidence)
        .ThenByDescending(s => s.SimilarCaseCount)
        .Take(MaxSuggestions)
        .ToArray();

    private static DepartmentSuggestion[] FromModel(
        RoutingAnswer answer, List<Department> departments, Dictionary<Guid, Evidence> evidence)
    {
        var bySymbol = departments.ToDictionary(d => d.Symbol, StringComparer.OrdinalIgnoreCase);
        return (answer.Departments ?? [])
            .Where(c => c?.Symbol is not null && bySymbol.ContainsKey(c.Symbol.Trim()))
            .Select(c => (Choice: c, Evidence: evidence[bySymbol[c.Symbol!.Trim()].Id]))
            .DistinctBy(x => x.Evidence.Department.Id)
            .Take(MaxSuggestions)
            .Select(x => Suggestion(
                x.Evidence,
                CleanReason(x.Choice.Reason) ?? FallbackReason(x.Evidence),
                double.IsFinite(x.Choice.Confidence) ? Math.Clamp(x.Choice.Confidence, 0, 1) : 0.5,
                SuggestionSource.Ai))
            .ToArray();
    }

    private static DepartmentSuggestion Suggestion(
        Evidence e, string reason, double confidence, SuggestionSource source) => new(
        e.Department.Id,
        e.Department.Symbol,
        e.Department.Name,
        reason,
        Math.Round(confidence, 2),
        e.SimilarCases,
        e.AverageResponseDays,
        source);

    private static string FallbackReason(Evidence e)
    {
        var keywordReason = e.ScopeFragment is null ? null : $"Zakres działania obejmuje: {e.ScopeFragment}";
        if (e.SimilarCases == 0)
        {
            return keywordReason ?? "Komórka właściwa według zakresu działania.";
        }

        var historyReason = HistoryReason(e);
        return keywordReason is not null && historyReason.Length + keywordReason.Length + 1 <= MaxReasonLength
            ? $"{historyReason} {keywordReason}"
            : historyReason;
    }

    private static string HistoryReason(Evidence e)
    {
        var cases = e.SimilarCases == 1 ? "W 1 podobnej sprawie" : $"W {e.SimilarCases} podobnych sprawach";
        var who = e.Department.Kind == DepartmentKind.Office
            ? "odpowiedzi udzielało to biuro"
            : "odpowiedzi udzielał ten wydział";
        var days = e.AverageResponseDays switch
        {
            null => "",
            var d when Math.Round(d.Value) == 1 => " (średnio w 1 dzień)",
            var d => $" (średnio w {Math.Round(d.Value).ToString(CultureInfo.InvariantCulture)} dni)",
        };
        return $"{cases} {who}{days}.";
    }

    private static string? CleanReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return null;
        }

        var clean = SearchText.CollapseWhitespace(reason);
        return clean.Length <= MaxReasonLength ? clean : clean[..(MaxReasonLength - 1)].TrimEnd() + "…";
    }

    private static string ScopeFragment(string scope, IReadOnlyList<string> stems)
    {
        var duties = scope.IndexOf("należy:", StringComparison.Ordinal);
        var body = duties >= 0 ? scope[(duties + "należy:".Length)..] : scope;
        return SearchText.Snippet(body, stems, MaxScopeFragment);
    }

    private string SystemPrompt() => $"""
        Jesteś asystentem urzędnika ({organization.Value.Name}), który rejestruje wnioski o udostępnienie informacji publicznej.
        Wskaż komórki organizacyjne urzędu (wydziały lub biura), które powinny przygotować informacje potrzebne do odpowiedzi na wniosek.
        Zasady:
        - Wybieraj wyłącznie komórki z katalogu (<katalog>), podając ich symbol dokładnie tak, jak w katalogu.
        - Wskaż od 1 do 3 komórek, zaczynając od najbardziej właściwej. Więcej niż jedną wskaż tylko wtedy, gdy wniosek dotyczy spraw kilku komórek.
        - Dla każdej komórki podaj uzasadnienie: jedno zdanie po polsku, najwyżej 200 znaków, oraz pewność w skali od 0 do 1.
        - Historia podobnych spraw (<historia>) jest pomocnicza; kieruj się przede wszystkim treścią wniosku i zakresem działania komórek.
        - {PromptText.UntrustedDataRule}
        """;

    private static string UserPrompt(string text, List<Department> departments, Dictionary<Guid, Evidence> evidence)
    {
        var catalog = new StringBuilder();
        foreach (var d in departments)
        {
            catalog.Append(d.Symbol).Append(" — ").Append(d.Name).Append(": ")
                .AppendLine(SearchText.CollapseWhitespace(PromptText.Truncate(d.Scope, MaxScopeInPrompt)));
        }

        var history = new StringBuilder();
        foreach (var e in evidence.Values.Where(e => e.SimilarCases > 0).OrderByDescending(e => e.SimilarCases))
        {
            history.Append(e.Department.Symbol).Append(": ").Append(e.SimilarCases).Append(" podobnych spraw");
            if (e.AverageResponseDays is { } days)
            {
                history.Append(", średni czas odpowiedzi ").Append(days.ToString(CultureInfo.InvariantCulture)).Append(" dni");
            }

            history.AppendLine();
        }

        return $"""
            {PromptText.Wrap("katalog", catalog.ToString(), 40_000)}

            {PromptText.Wrap("historia", history.Length == 0 ? "Brak podobnych spraw w rejestrze." : history.ToString(), 4000)}

            {PromptText.Wrap("wniosek", text, MaxRequestInPrompt)}
            """;
    }

    private sealed record Evidence(
        Department Department,
        int SimilarCases,
        int CasesWithDepartments,
        double? AverageResponseDays,
        double KeywordScore,
        string? ScopeFragment)
    {
        public double Confidence => SimilarCases > 0
            ? Math.Min(0.95, 0.5 + 0.4 * SimilarCases / Math.Max(CasesWithDepartments, 3) + 0.05 * KeywordScore)
            : 0.5 * KeywordScore;
    }
}
