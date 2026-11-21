using System.ComponentModel;
using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Features.Requests;
using ClerkAssistant.Api.Features.Search;
using ClerkAssistant.Api.Infrastructure.Ai;
using ClerkAssistant.Api.Infrastructure.Common;
using ClerkAssistant.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;

namespace ClerkAssistant.Api.Features.Assistant;

public sealed class AssistantLookups(
    AppDbContext db, CaseFactsLoader facts, RequestRegisterService register, HybridSearch search, CurrentUser user)
{
    public const string FindRequestsTool = "find_requests";
    public const string GetCaseTool = "get_case";
    public const string SearchArchiveTool = "search_archive";
    public const string FindDepartmentsTool = "find_departments";

    private const int MaxRequests = 10;
    private const int MaxSearchHits = 5;
    private const int MaxDepartments = 8;
    private const int MaxPhraseLength = 200;
    private const int MaxResultLength = 6000;
    private const int MaxSubjectLength = 160;
    private const int MaxScopeLength = 240;
    private const int SymbolMatchScore = 100;
    private const int NameMatchScore = 3;

    public IEnumerable<AITool> Functions() =>
    [
        AIFunctionFactory.Create(FindRequestsAsync, FindRequestsTool,
            "Wyszukuje sprawy w rejestrze wniosków (bieżący rok i otwarte sprawy z lat poprzednich). " +
            "Zwraca znak sprawy, przedmiot, termin, status, wydziały i adres strony sprawy."),
        AIFunctionFactory.Create(GetCaseAsync, GetCaseTool,
            "Zwraca fakty o sprawie o podanym znaku: status, terminy, możliwość przedłużenia terminu, wydziały, " +
            "projekt odpowiedzi, to, co blokuje kolejny krok, oraz zalecany kolejny krok."),
        AIFunctionFactory.Create(SearchArchiveAsync, SearchArchiveTool,
            "Przeszukuje rejestr, archiwalne odpowiedzi i bazę wiedzy (akty prawne, zarządzenia, wytyczne) — " +
            "wyszukiwanie pełnotekstowe i semantyczne. Używaj do szukania podobnych spraw i przepisów."),
        AIFunctionFactory.Create(FindDepartmentsAsync, FindDepartmentsTool,
            "Wyszukuje wydziały i biura urzędu po symbolu, nazwie albo zakresie działania. " +
            "Bez frazy zwraca listę wszystkich aktywnych wydziałów."),
    ];

    public static string FormatHits(IReadOnlyCollection<SearchHit> hits) =>
        hits.Count == 0
            ? "Brak wyników."
            : PromptText.Wrap("wyniki_wyszukiwania", string.Join("\n", hits.Select(FormatHit)), MaxResultLength);

    private async Task<string> FindRequestsAsync(
        [Description("Fraza: znak sprawy, fragment przedmiotu albo nazwisko wnioskodawcy. Opcjonalnie.")] string? phrase = null,
        [Description("Status: NoAction, QueriesSent, Extended, ResponsePrepared albo ResponseSent. Opcjonalnie.")] string? status = null,
        [Description("true — tylko sprawy prowadzone przez bieżącego użytkownika.")] bool onlyMine = false,
        [Description("Pilność: Overdue (po terminie), DueToday, DueTomorrow, DueSoon (do 3 dni), OnTrack albo Closed. Opcjonalnie.")] string? urgency = null,
        CancellationToken ct = default)
    {
        var statusFilter = ToolArguments.ParseEnum<RequestStatus>(status);
        var urgencyFilter = ToolArguments.ParseEnum<Urgency>(urgency);
        if (statusFilter.Invalid || urgencyFilter.Invalid)
        {
            return "Nieprawidłowy status albo pilność — użyj jednej z wartości podanych w opisie narzędzia.";
        }

        var search = string.IsNullOrWhiteSpace(phrase) ? null : AssistantWording.Clip(phrase, MaxPhraseLength);
        var found = await register.ListAsync(
            new RegisterFilter(null, statusFilter.Value, onlyMine ? user.Id : null, search, null), ct);
        var matching = found.Where(r => urgencyFilter.Value is null || r.Urgency == urgencyFilter.Value).ToList();
        if (matching.Count == 0)
        {
            return "Nie znaleziono spraw spełniających kryteria.";
        }

        var lines = matching.Take(MaxRequests).Select(r =>
            $"- {r.CaseNumber}: {AssistantWording.Clip(r.Subject, MaxSubjectLength)} — " +
            $"status {AssistantWording.Quoted(AssistantWording.Status(r.Status))}, " +
            (r.Status == RequestStatus.ResponseSent
                ? $"odpowiedź wysłana {AssistantWording.Date(r.RespondedOn)}"
                : $"termin {AssistantWording.Date(r.EffectiveDueOn)} ({AssistantWording.DaysLeft(r.DaysLeft)})") +
            $", wydziały: {Symbols(r.DepartmentSymbols)}, strona: /wnioski/{r.Id}");
        var more = matching.Count > MaxRequests ? $"\nPokazano {MaxRequests} z {matching.Count} spraw — zawęź kryteria." : "";
        return PromptText.Wrap("sprawy", string.Join("\n", lines), MaxResultLength) + more;
    }

    private async Task<string> GetCaseAsync(
        [Description("Znak sprawy, np. BKS.IV.1431.3.2026.")] string caseNumber,
        CancellationToken ct = default)
    {
        var sheet = await facts.FindRequestIdAsync(caseNumber, ct) is { } id ? await facts.ForRequestAsync(id, ct) : null;
        return sheet is null ? ToolArguments.CaseNotFound(caseNumber) : CaseFacts.ToPrompt(sheet);
    }

    private async Task<string> SearchArchiveAsync(
        [Description("Czego szukać — słowa kluczowe albo opis sprawy.")] string query,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return "Podaj, czego szukać.";
        }

        var outcome = await search.SearchAsync(
            new SearchRequest(AssistantWording.Clip(query, SearchService.MaxQueryLength)) { Limit = MaxSearchHits }, ct);
        return FormatHits(outcome.Hits);
    }

    private async Task<string> FindDepartmentsAsync(
        [Description("Symbol, nazwa albo zakres działania (np. „drogi”, „WGK”). Opcjonalnie.")] string? query = null,
        CancellationToken ct = default)
    {
        var departments = await db.Departments.AsNoTracking().Where(d => d.IsActive).OrderBy(d => d.Symbol).ToListAsync(ct);
        if (string.IsNullOrWhiteSpace(query))
        {
            return string.Join("\n", departments.Select(d => $"- {d.Symbol} — {d.Name}"));
        }

        var wanted = query.Trim();
        var stems = SearchText.Stems(SearchText.QueryTokens(wanted, 3, includeNumbers: false).Select(t => t.Token));
        var ranked = departments
            .Select(d => (Department: d, Score: DepartmentScore(d, wanted, stems)))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Take(MaxDepartments)
            .Select(x => x.Department)
            .ToList();
        return ranked.Count == 0
            ? "Nie znaleziono pasujących wydziałów. Wywołaj narzędzie bez frazy, aby zobaczyć wszystkie wydziały."
            : string.Join("\n", ranked.Select(d =>
                $"- {d.Symbol} — {d.Name}{(d.Email is null ? "" : $" ({d.Email})")}: zakres: {AssistantWording.Clip(d.Scope, MaxScopeLength)}"));
    }

    private static int DepartmentScore(Department department, string query, IReadOnlyList<string> stems)
    {
        if (string.Equals(department.Symbol, query, StringComparison.OrdinalIgnoreCase))
        {
            return SymbolMatchScore;
        }

        var name = SearchText.QueryTokens(department.Name, 3, includeNumbers: false).Select(t => t.Token).ToList();
        var scope = SearchText.QueryTokens(department.Scope, 3, includeNumbers: false).Select(t => t.Token).ToList();
        return stems.Sum(stem =>
            name.Any(w => w.StartsWith(stem, StringComparison.Ordinal)) ? NameMatchScore
            : scope.Any(w => w.StartsWith(stem, StringComparison.Ordinal)) ? 1
            : 0);
    }

    private static string FormatHit(SearchHit hit) => hit.Kind == SearchHitKind.Request
        ? $"- Sprawa {hit.CaseNumber} z {AssistantWording.Date(hit.Date)} " +
          $"(status {AssistantWording.Quoted(AssistantWording.Status(hit.Status ?? RequestStatus.NoAction))}, " +
          $"wydziały: {Symbols(hit.DepartmentSymbols)}): {AssistantWording.Clip(hit.Title, MaxSubjectLength)} — {hit.Snippet} " +
          $"(strona: /wnioski/{hit.Id})"
        : $"- {AssistantWording.KnowledgeKind(hit.KnowledgeKind)}: {AssistantWording.Clip(hit.Title, MaxSubjectLength)} — " +
          $"{hit.Snippet} (baza wiedzy: /baza-wiedzy)";

    private static string Symbols(string[] symbols) => symbols.Length == 0 ? "brak" : string.Join(", ", symbols);
}
