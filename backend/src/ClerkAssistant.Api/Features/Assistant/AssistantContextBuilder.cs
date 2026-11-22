using System.Text;
using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Features.Requests;
using ClerkAssistant.Api.Features.Search;
using ClerkAssistant.Api.Infrastructure.Ai;
using ClerkAssistant.Api.Infrastructure.Common;
using ClerkAssistant.Api.Infrastructure.Letters;
using ClerkAssistant.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace ClerkAssistant.Api.Features.Assistant;

public sealed record AssistantSituation(ScreenContext Screen, GuideSection? Page, CaseFactSheet? Case);

public sealed class AssistantContextBuilder(
    AppDbContext db,
    AppGuide guide,
    CaseFactsLoader facts,
    RequestRegisterService register,
    HybridSearch search,
    CurrentUser user,
    AiGateway ai,
    AppClock clock,
    IOptions<OrganizationOptions> organization)
{
    public const int MaxOwnCases = 10;
    public const int SearchHits = 5;

    private const int MaxScreenInPrompt = 8000;
    private const int MaxHistoryMessageLength = 6000;
    private const int MaxSubjectInList = 160;

    public async Task<AssistantSituation> DescribeAsync(ScreenContext screen, CancellationToken ct)
    {
        var caseSheet = CaseFactsLoader.RequestIdFromPath(screen.Path) is { } requestId
            ? await facts.ForRequestAsync(requestId, ct)
            : null;
        return new AssistantSituation(screen, guide.PageFor(screen.Path), caseSheet);
    }

    public async Task<List<ChatMessage>> MessagesAsync(
        AssistantSituation situation, IReadOnlyList<AssistantMessage> history, string question, CancellationToken ct)
    {
        var messages = new List<ChatMessage> { new(ChatRole.System, await SystemPromptAsync(situation, question, ct)) };
        messages.AddRange(history.Select(m => new ChatMessage(
            m.Role == AssistantRole.User ? ChatRole.User : ChatRole.Assistant,
            PromptText.Truncate(m.Content, MaxHistoryMessageLength))));
        return messages;
    }

    private async Task<string> SystemPromptAsync(AssistantSituation situation, string question, CancellationToken ct)
    {
        var prompt = new StringBuilder()
            .AppendLine(Rules(ai.CanUseTools))
            .AppendLine()
            .AppendLine(await UserLineAsync(ct))
            .AppendLine()
            .AppendLine("Przewodnik po aplikacji (zaufane źródło wiedzy o działaniu aplikacji i zasadach obsługi wniosków):")
            .AppendLine(guide.Text)
            .AppendLine()
            .AppendLine(Screen(situation))
            .AppendLine();
        if (situation.Case is { } sheet)
        {
            prompt.AppendLine("Sprawa otwarta na ekranie użytkownika (dane z rejestru):")
                .AppendLine(CaseFacts.ToPrompt(sheet))
                .AppendLine();
        }

        prompt.AppendLine(await RegisterAsync(ct));
        if (!ai.CanUseTools)
        {
            prompt.AppendLine().AppendLine(await SearchAsync(question, ct));
        }

        return prompt.ToString().TrimEnd();
    }

    private string Rules(bool tools)
    {
        var org = organization.Value;
        var actions = tools
            ? "- Dane z rejestru, archiwum, bazy wiedzy i słownika wydziałów sprawdzaj narzędziami. Zmiany w sprawach " +
              "(status, przedłużenie terminu, wydziały, daty zapytań i odpowiedzi wydziałów) możesz wyłącznie zaproponować " +
              "narzędziami propose_*. Propozycja pojawi się jako karta w oknie asystenta i zostanie wykonana dopiero po " +
              "zatwierdzeniu przez urzędnika. Po przygotowaniu propozycji napisz krótko, co proponujesz, i poproś o jej sprawdzenie."
            : "- W tym trybie nie masz dostępu do narzędzi i nie możesz proponować działań. Opisz krok po kroku, jak urzędnik " +
              "może wykonać działanie samodzielnie w aplikacji. Poniżej znajdziesz wyniki wyszukiwania dla pytania.";
        return $"""
            Jesteś asystentem w aplikacji „SOIP — System Obsługi Informacji Publicznej”, z której korzystają pracownicy ({org.Name}, {org.UnitName}) obsługujący wnioski o udostępnienie informacji publicznej. Pomagasz korzystać z aplikacji, wyjaśniasz zasady i terminy oraz podpowiadasz kolejne kroki w sprawach.

            Zasady:
            - Odpowiadaj po polsku, zwięźle i rzeczowo.
            - Formatowanie: akapity oddzielaj pustą linią, listy zaczynaj od „- ” albo „1. ”, możesz używać **pogrubienia** i `kodu`. Linki podawaj wyłącznie w postaci [tekst](/ścieżka) do stron aplikacji — ścieżka zaczyna się od jednego ukośnika, np. [rejestr wniosków](/). Nie używaj nagłówków, tabel, obrazów, HTML ani adresów zewnętrznych.
            - Opieraj się wyłącznie na przewodniku, danych poniżej i wynikach narzędzi. Nigdy nie wymyślaj znaków spraw, dat, nazwisk, liczb ani przepisów. Jeśli nie masz pewności albo brakuje danych, powiedz to wprost i wskaż, gdzie w aplikacji można to sprawdzić.
            - Nigdy nie twierdź, że wykonałeś jakieś działanie. Aplikacja niczego nie wysyła sama — pisma i odpowiedzi urzędnik wysyła samodzielnie, a decyzję zawsze podejmuje urzędnik.
            {actions}
            - {PromptText.UntrustedDataRule}
            """;
    }

    private async Task<string> UserLineAsync(CancellationToken ct)
    {
        var userId = user.Id;
        var fullName = await db.Users.Where(u => u.Id == userId).Select(u => u.FullName).FirstOrDefaultAsync(ct) ?? user.UserName;
        var role = user.IsInRole(Roles.Admin)
            ? "administrator — ma dostęp także do funkcji administracyjnych (użytkownicy, dziennik zdarzeń)"
            : "urzędnik — nie ma dostępu do funkcji administracyjnych (użytkownicy, dziennik zdarzeń); jeśli o nie pyta, wyjaśnij, że wymagają uprawnień administratora";
        return $"Dzisiaj jest {LetterLayout.LongDate(clock.Today)} Rozmawiasz z użytkownikiem: {fullName} ({role}).";
    }

    private static string Screen(AssistantSituation situation)
    {
        var screen = situation.Screen;
        var details = new StringBuilder().AppendLine($"Adres: {screen.Path}");
        AppendIf(details, "Tytuł strony", screen.Title);
        AppendIf(details, "Nagłówek", screen.Heading);
        AppendIf(details, "Otwarte okno", screen.Dialog);
        if (screen.FieldErrors is { Length: > 0 } errors)
        {
            details.AppendLine("Błędy pól formularza:");
            foreach (var error in errors)
            {
                details.AppendLine(string.IsNullOrWhiteSpace(error.Label) ? $"- {error.Message}" : $"- {error.Label}: {error.Message}");
            }
        }

        if (screen.Alerts is { Length: > 0 } alerts)
        {
            details.AppendLine("Komunikaty widoczne na ekranie:");
            foreach (var alert in alerts)
            {
                details.AppendLine($"- {alert}");
            }
        }

        var page = situation.Page is { } section ? AssistantWording.Quoted(section.Title) : "nieznana";
        return $"Ekran, który użytkownik ma teraz przed sobą (strona według przewodnika: {page}; dane z przeglądarki, niezweryfikowane):\n" +
               PromptText.Wrap("ekran", details.ToString().TrimEnd(), MaxScreenInPrompt);
    }

    private static void AppendIf(StringBuilder text, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            text.AppendLine($"{label}: {value}");
        }
    }

    private async Task<string> RegisterAsync(CancellationToken ct)
    {
        var summary = await register.SummaryAsync(null, ct);
        var own = (await register.ListAsync(new RegisterFilter(null, null, user.Id, null, null), ct))
            .Where(r => r.Urgency != Urgency.Closed)
            .Take(MaxOwnCases)
            .ToList();
        var text = new StringBuilder()
            .AppendLine($"Rejestr wniosków (bieżący rok i otwarte sprawy z lat poprzednich): po terminie {summary.Overdue}, " +
                        $"termin dziś {summary.DueToday}, termin jutro {summary.DueTomorrow}, termin w ciągu 3 dni {summary.DueSoon}, " +
                        $"otwarte {summary.Open}, zakończone {summary.Closed}, razem {summary.Total}.");
        if (own.Count == 0)
        {
            return text.Append("Użytkownik nie prowadzi obecnie otwartych spraw.").ToString();
        }

        var lines = own.Select(r =>
            $"- {r.CaseNumber}: {AssistantWording.Clip(r.Subject, MaxSubjectInList)} — " +
            $"termin {AssistantWording.Date(r.EffectiveDueOn)} ({AssistantWording.DaysLeft(r.DaysLeft)}), " +
            $"status {AssistantWording.Quoted(AssistantWording.Status(r.Status))}, strona: /wnioski/{r.Id}");
        return text
            .AppendLine($"Najpilniejsze otwarte sprawy prowadzone przez użytkownika (najwyżej {MaxOwnCases}):")
            .Append(PromptText.Wrap("sprawy_uzytkownika", string.Join("\n", lines), 4000))
            .ToString();
    }

    private async Task<string> SearchAsync(string question, CancellationToken ct)
    {
        var outcome = await search.SearchAsync(
            new SearchRequest(AssistantWording.Clip(question, SearchService.MaxQueryLength)) { Limit = SearchHits }, ct);
        return "Wyniki wyszukiwania w rejestrze, archiwum i bazie wiedzy dla pytania użytkownika (mogą być nietrafne):\n" +
               AssistantLookups.FormatHits(outcome.Hits);
    }
}
