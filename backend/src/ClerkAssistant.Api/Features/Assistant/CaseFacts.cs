using System.Text;
using System.Text.RegularExpressions;
using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Features.Requests;
using ClerkAssistant.Api.Infrastructure.Ai;

namespace ClerkAssistant.Api.Features.Assistant;

public sealed record CaseSnapshot(RequestDetail Request, string? DraftContent);

public sealed record CaseFactSheet(
    Guid RequestId,
    string CaseNumber,
    IReadOnlyList<string> Facts,
    IReadOnlyList<string> Blockers,
    string NextStep,
    string Subject,
    string Content)
{
    public string Link => $"/wnioski/{RequestId}";

    public string DraftLink => $"{Link}/odpowiedz";
}

public static partial class CaseFacts
{
    private const int UrgentDays = 3;
    private const int MaxSubjectInPrompt = 500;
    private const int MaxContentInPrompt = 3000;

    public static int CountPlaceholders(string? text) => text is null ? 0 : Placeholder().Count(text);

    public static CaseFactSheet Describe(CaseSnapshot snapshot)
    {
        var request = snapshot.Request;
        var situation = new Situation(request, snapshot.DraftContent);
        return new CaseFactSheet(
            request.Id,
            request.CaseNumber,
            Facts(situation),
            Blockers(situation),
            NextStep(situation),
            request.Subject,
            request.Content);
    }

    public static string ToPrompt(CaseFactSheet sheet)
    {
        var text = new StringBuilder()
            .AppendLine($"Sprawa {sheet.CaseNumber} (strona sprawy: {sheet.Link}, projekt odpowiedzi: {sheet.DraftLink}):");
        foreach (var fact in sheet.Facts)
        {
            text.AppendLine($"- {fact}");
        }

        text.AppendLine(sheet.Blockers.Count == 0 ? "Nic nie blokuje kolejnego kroku." : "Co blokuje kolejny krok:");
        foreach (var blocker in sheet.Blockers)
        {
            text.AppendLine($"- {blocker}");
        }

        return text
            .AppendLine($"Zalecany kolejny krok: {sheet.NextStep}")
            .AppendLine(PromptText.Wrap("przedmiot_wniosku", sheet.Subject, MaxSubjectInPrompt))
            .Append(PromptText.Wrap("tresc_wniosku", sheet.Content, MaxContentInPrompt))
            .ToString();
    }

    private static List<string> Facts(Situation s)
    {
        var request = s.Request;
        return
        [
            $"Znak sprawy: {request.CaseNumber}",
            $"Status: {AssistantWording.Quoted(AssistantWording.Status(request.Status))}",
            $"Data wpływu: {AssistantWording.Date(request.ReceivedOn)}",
            Deadline(s),
            Extension(s),
            $"Osoba prowadząca: {request.Assignee?.FullName ?? "nie wskazano"}",
            request.Departments.Length == 0
                ? "Wydziały: nie przypisano"
                : "Wydziały: " + string.Join("; ", request.Departments.Select(DepartmentLine)),
            $"Pliki z odpowiedziami wydziałów: {s.Replies}",
            Draft(s),
        ];
    }

    private static string Deadline(Situation s)
    {
        var request = s.Request;
        if (s.Closed)
        {
            return $"Odpowiedź wysłana: {AssistantWording.Date(request.RespondedOn)}";
        }

        var extended = request.ExtendedDueOn is not null
            ? $" (przedłużony z {AssistantWording.Date(request.DueOn)}, " +
              $"rodzaj: {AssistantWording.ExtensionKind(request.ExtensionKind ?? ExtensionKind.Query)})"
            : "";
        return $"Termin odpowiedzi: {AssistantWording.Date(request.EffectiveDueOn)}{extended} — {AssistantWording.DaysLeft(request.DaysLeft)}";
    }

    private static string Extension(Situation s)
    {
        var latest = AssistantWording.Date(s.Request.LatestExtendedDueOn);
        if (s.Closed)
        {
            return "Przedłużenie terminu: niemożliwe — sprawa jest zakończona";
        }

        if (!s.CanExtend)
        {
            return $"Przedłużenie terminu: niemożliwe — limit 2 miesięcy od dnia wpływu upływa {latest}";
        }

        return s.Request.ExtendedDueOn is null
            ? $"Przedłużenie terminu: możliwe najpóźniej do {latest} (2 miesiące od dnia wpływu, art. 13 ust. 2 UDIP)"
            : $"Przedłużenie terminu: termin już przedłużono; nowy termin można ustalić najpóźniej na {latest}";
    }

    private static string DepartmentLine(RequestDepartmentDto department)
    {
        var asked = department.AskedOn is { } askedOn
            ? $"zapytanie wysłane {AssistantWording.Date(askedOn)}"
            : "zapytanie niewysłane";
        var answered = department.AnsweredOn is { } answeredOn
            ? $"odpowiedź {AssistantWording.Date(answeredOn)}"
            : "brak odpowiedzi";
        return $"{department.Symbol} ({department.Name}): {asked}, {answered}";
    }

    private static string Draft(Situation s)
    {
        if (s.Request.Draft is not { } draft)
        {
            return "Projekt odpowiedzi: brak";
        }

        var origin = draft.GeneratedByAi ? "przygotowany z pomocą AI" : "przygotowany z szablonu lub ręcznie";
        return $"Projekt odpowiedzi: jest ({origin}); sugerowane anonimizacje do decyzji: {draft.SuggestedRedactions}; " +
               $"zatwierdzone anonimizacje: {draft.ApprovedRedactions}; miejsca „DO UZUPEŁNIENIA”: {s.Placeholders}";
    }

    private static List<string> Blockers(Situation s)
    {
        var blockers = new List<string>();
        if (s.Closed)
        {
            return blockers;
        }

        if (s.Request.DaysLeft < 0)
        {
            blockers.Add($"Termin odpowiedzi minął {AssistantWording.Days(-s.Request.DaysLeft)} temu.");
        }

        if (s.Request.Departments.Length == 0)
        {
            blockers.Add("Do sprawy nie przypisano wydziałów.");
        }

        if (s.NotAsked.Count > 0)
        {
            blockers.Add($"Nie zapisano wysłania zapytania do: {string.Join(", ", s.NotAsked)}.");
        }

        if (s.Unanswered.Count > 0)
        {
            blockers.Add($"Brak odpowiedzi od: {string.Join(", ", s.Unanswered)}.");
        }

        if (s.Request.Departments.Length > 0 && s.Replies == 0)
        {
            blockers.Add("Nie dołączono plików z odpowiedziami wydziałów.");
        }

        if (s.Placeholders > 0)
        {
            blockers.Add($"W projekcie odpowiedzi są niewypełnione miejsca „DO UZUPEŁNIENIA” (liczba: {s.Placeholders}).");
        }

        if (s.Request.Draft is { SuggestedRedactions: > 0 } draft)
        {
            blockers.Add($"Sugerowane anonimizacje czekają na decyzję (liczba: {draft.SuggestedRedactions}).");
        }

        return blockers;
    }

    private static string NextStep(Situation s)
    {
        var request = s.Request;
        if (s.Closed)
        {
            return "Sprawa jest zakończona — dalsze działania nie są potrzebne.";
        }

        if (request.Departments.Length == 0)
        {
            return "Przypisz wydziały właściwe dla wniosku i wyślij do nich zapytanie.";
        }

        if (s.NotAsked.Count > 0)
        {
            return "Wyślij zapytanie do wydziałów (treść zapytania przygotujesz na stronie sprawy) i zapisz datę wysłania.";
        }

        if (s.Unanswered.Count > 0)
        {
            return request.DaysLeft switch
            {
                < 0 => "Termin odpowiedzi minął, a wydziały jeszcze nie odpowiedziały — ponaglij je i udziel odpowiedzi jak najszybciej.",
                <= UrgentDays when s.CanExtend =>
                    "Wydziały jeszcze nie odpowiedziały, a termin jest bliski — rozważ przedłużenie terminu (kwerenda) i zawiadom wnioskodawcę.",
                _ => "Poczekaj na odpowiedzi wydziałów i zapisz datę ich otrzymania.",
            };
        }

        if (s.Replies == 0)
        {
            return "Dołącz pliki z odpowiedziami wydziałów, aby przygotować projekt odpowiedzi.";
        }

        if (request.Draft is null)
        {
            return $"Przygotuj [projekt odpowiedzi]({s.DraftLink}).";
        }

        if (s.Placeholders > 0 || request.Draft.SuggestedRedactions > 0)
        {
            return $"Uzupełnij miejsca „DO UZUPEŁNIENIA” i rozpatrz sugerowane anonimizacje w [projekcie odpowiedzi]({s.DraftLink}).";
        }

        return request.Status == RequestStatus.ResponsePrepared
            ? "Wyślij odpowiedź wnioskodawcy (aplikacja niczego nie wysyła sama) i oznacz sprawę jako „Odpowiedź wysłana”, podając datę wysłania."
            : "Sprawdź projekt odpowiedzi, pobierz pismo i oznacz sprawę jako „Odpowiedź przygotowana”.";
    }

    [GeneratedRegex(@"\[DO UZUPEŁNIENIA[^\]]*\]")]
    private static partial Regex Placeholder();

    private sealed class Situation(RequestDetail request, string? draftContent)
    {
        public RequestDetail Request { get; } = request;
        public bool Closed => Request.Status == RequestStatus.ResponseSent;
        public bool CanExtend => !Closed && Deadlines.ExtendedDueDate(Request.DueOn, 1) <= Request.LatestExtendedDueOn;
        public int Replies { get; } = request.Documents.Count(d => d.Kind == DocumentKind.DepartmentReply);
        public int Placeholders { get; } = CountPlaceholders(draftContent);
        public string DraftLink => $"/wnioski/{Request.Id}/odpowiedz";
        public List<string> NotAsked { get; } = request.Departments.Where(d => d.AskedOn is null).Select(d => d.Symbol).ToList();
        public List<string> Unanswered { get; } =
            request.Departments.Where(d => d.AskedOn is not null && d.AnsweredOn is null).Select(d => d.Symbol).ToList();
    }
}
