using System.ComponentModel;
using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Features.Requests;
using ClerkAssistant.Api.Infrastructure.Common;
using ClerkAssistant.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;

namespace ClerkAssistant.Api.Features.Assistant;

public sealed class AssistantProposals(AppDbContext db, AppClock clock)
{
    public const string StatusTool = "propose_status_change";
    public const string ExtensionTool = "propose_deadline_extension";
    public const string DepartmentsTool = "propose_departments";
    public const string DepartmentDatesTool = "propose_department_dates";
    public const int MaxProposals = 5;

    private const int MaxReasonInDetails = 300;
    private const string InvalidDate = "Nieprawidłowa data — użyj formatu RRRR-MM-DD.";

    public List<AssistantAction> Proposed { get; } = [];

    public IEnumerable<AITool> Functions() =>
    [
        AIFunctionFactory.Create(ProposeStatusChangeAsync, StatusTool,
            "Przygotowuje propozycję zmiany statusu sprawy. Zmiana zostanie wykonana dopiero po zatwierdzeniu przez urzędnika."),
        AIFunctionFactory.Create(ProposeDeadlineExtensionAsync, ExtensionTool,
            "Przygotowuje propozycję przedłużenia terminu odpowiedzi (art. 13 ust. 2 UDIP, najwyżej 2 miesiące od dnia wpływu). " +
            "Po zatwierdzeniu powstaje projekt zawiadomienia albo wezwania dla wnioskodawcy."),
        AIFunctionFactory.Create(ProposeDepartmentsAsync, DepartmentsTool,
            "Przygotowuje propozycję dodania lub usunięcia wydziałów przypisanych do sprawy (symbole wydziałów, np. WGK)."),
        AIFunctionFactory.Create(ProposeDepartmentDatesAsync, DepartmentDatesTool,
            "Przygotowuje propozycję zapisania daty wysłania zapytania do wydziału lub daty odpowiedzi wydziału."),
    ];

    private Task<string> ProposeStatusChangeAsync(
        [Description("Znak sprawy.")] string caseNumber,
        [Description("Nowy status: NoAction, QueriesSent, ResponsePrepared albo ResponseSent. Do przedłużenia terminu służy osobne narzędzie.")] string status,
        [Description("Data wysłania odpowiedzi (RRRR-MM-DD) — tylko dla ResponseSent; domyślnie dzisiaj.")] string? respondedOn = null,
        CancellationToken ct = default) =>
        ProposeAsync(caseNumber, request =>
        {
            var target = ToolArguments.ParseEnum<RequestStatus>(status).Value
                ?? throw ValidationException.For("status", RequestRules.UnknownStatus);
            var sentOn = RequestWorkflowService.CheckStatusChange(request, target, Date(respondedOn, "respondedOn"), clock.Today);
            if (target == request.Status && sentOn == request.RespondedOn)
            {
                throw ValidationException.For("status", "Sprawa ma już ten status.");
            }

            var details = new List<string>
            {
                $"Obecny status: {AssistantWording.Quoted(AssistantWording.Status(request.Status))}",
                $"Nowy status: {AssistantWording.Quoted(AssistantWording.Status(target))}",
            };
            if (sentOn is { } date)
            {
                details.Add($"Data wysłania odpowiedzi: {AssistantWording.Date(date)}");
            }

            return Task.FromResult(new Proposal(
                AssistantActionKind.ChangeStatus,
                $"Zmiana statusu sprawy {request.CaseNumber} na {AssistantWording.Quoted(AssistantWording.Status(target))}",
                details,
                new AssistantActionArguments { Status = target, RespondedOn = sentOn }));
        }, ct);

    private Task<string> ProposeDeadlineExtensionAsync(
        [Description("Znak sprawy.")] string caseNumber,
        [Description("O ile dni przedłużyć termin, licząc od pierwotnego terminu odpowiedzi.")] int days,
        [Description("Powód przedłużenia (przyczyna opóźnienia), który trafi do pisma dla wnioskodawcy.")] string reason,
        [Description("Rodzaj: Query (kwerenda, domyślnie) albo Clarification (wezwanie do doprecyzowania wniosku).")] string? kind = null,
        CancellationToken ct = default) =>
        ProposeAsync(caseNumber, request =>
        {
            var parsedKind = ToolArguments.ParseEnum<ExtensionKind>(kind);
            if (parsedKind.Invalid)
            {
                throw ValidationException.For("kind", "Nieprawidłowy rodzaj przedłużenia terminu.");
            }

            var arguments = new AssistantActionArguments
            {
                Days = days,
                Reason = reason?.Trim() ?? "",
                ExtensionKind = parsedKind.Value ?? ExtensionKind.Query,
            };
            var extendedDueOn = RequestWorkflowService.CheckExtension(request, arguments.ToExtendDeadlineRequest(request.Version));
            return Task.FromResult(new Proposal(
                AssistantActionKind.ExtendDeadline,
                $"Przedłużenie terminu w sprawie {request.CaseNumber} do {AssistantWording.Date(extendedDueOn)}",
                [
                    $"Obecny termin: {AssistantWording.Date(request.EffectiveDueOn)}",
                    $"Nowy termin: {AssistantWording.Date(extendedDueOn)} (pierwotny termin {AssistantWording.Date(request.DueOn)} + {AssistantWording.Days(days)})",
                    $"Rodzaj: {AssistantWording.ExtensionKind(arguments.ExtensionKind.Value)}",
                    $"Powód: {AssistantWording.Clip(arguments.Reason, MaxReasonInDetails)}",
                    "Po zatwierdzeniu otrzymasz projekt pisma dla wnioskodawcy do skopiowania.",
                ],
                arguments));
        }, ct);

    private Task<string> ProposeDepartmentsAsync(
        [Description("Znak sprawy.")] string caseNumber,
        [Description("Symbole wydziałów do dodania, np. [\"WGK\"].")] string[]? add = null,
        [Description("Symbole wydziałów do usunięcia.")] string[]? remove = null,
        CancellationToken ct = default) =>
        ProposeAsync(caseNumber, async request =>
        {
            var toAdd = Symbols(add);
            var toRemove = Symbols(remove);
            if (toAdd.Count == 0 && toRemove.Count == 0)
            {
                throw ValidationException.For("departments", "Podaj symbole wydziałów do dodania lub usunięcia.");
            }

            var departments = await db.Departments.AsNoTracking().ToListAsync(ct);
            var bySymbol = departments.ToDictionary(d => d.Symbol.ToUpperInvariant());
            var unknown = toAdd.Concat(toRemove).Where(s => !bySymbol.ContainsKey(s)).ToList();
            if (unknown.Count > 0)
            {
                throw ValidationException.For("departments",
                    $"Nieznane symbole wydziałów: {string.Join(", ", unknown)}. Sprawdź je narzędziem {AssistantLookups.FindDepartmentsTool}.");
            }

            var current = await db.RequestDepartments.AsNoTracking()
                .Where(l => l.RequestId == request.Id)
                .Select(l => l.DepartmentId)
                .ToListAsync(ct);
            var removed = toRemove.Select(s => bySymbol[s].Id).ToHashSet();
            var final = current.Concat(toAdd.Select(s => bySymbol[s].Id)).Where(id => !removed.Contains(id)).Distinct().ToArray();
            if (final.ToHashSet().SetEquals(current))
            {
                throw ValidationException.For("departments", "Ta zmiana nie zmienia wydziałów przypisanych do sprawy.");
            }

            var errors = new ValidationErrors();
            await errors.CheckDepartmentsAsync(db, final, ct);
            errors.ThrowIfAny();

            var byId = departments.ToDictionary(d => d.Id);
            var added = final.Except(current).Select(id => byId[id]).OrderBy(d => d.Symbol, StringComparer.Ordinal).ToList();
            var dropped = current.Except(final).Select(id => byId[id]).OrderBy(d => d.Symbol, StringComparer.Ordinal).ToList();
            var after = final.Select(id => byId[id].Symbol).OrderBy(s => s, StringComparer.Ordinal).ToList();
            var change = string.Join(", ", added.Select(d => $"+{d.Symbol}").Concat(dropped.Select(d => $"−{d.Symbol}")));
            return new Proposal(
                AssistantActionKind.SetDepartments,
                $"Zmiana wydziałów w sprawie {request.CaseNumber}: {change}",
                [
                    .. added.Select(d => $"Dodanie: {d.Symbol} — {d.Name}"),
                    .. dropped.Select(d => $"Usunięcie: {d.Symbol} — {d.Name}"),
                    $"Wydziały po zmianie: {(after.Count == 0 ? "brak" : string.Join(", ", after))}",
                ],
                new AssistantActionArguments { DepartmentIds = final, PreviousDepartmentIds = current.ToArray() });
        }, ct);

    private Task<string> ProposeDepartmentDatesAsync(
        [Description("Znak sprawy.")] string caseNumber,
        [Description("Symbol wydziału przypisanego do sprawy, np. WGK.")] string departmentSymbol,
        [Description("Data wysłania zapytania do wydziału (RRRR-MM-DD). Opcjonalnie.")] string? askedOn = null,
        [Description("Data otrzymania odpowiedzi wydziału (RRRR-MM-DD). Opcjonalnie.")] string? answeredOn = null,
        CancellationToken ct = default) =>
        ProposeAsync(caseNumber, async request =>
        {
            var symbol = departmentSymbol?.Trim().ToUpperInvariant() ?? "";
            var link = await db.RequestDepartments.AsNoTracking()
                .Include(l => l.Department)
                .FirstOrDefaultAsync(l => l.RequestId == request.Id && l.Department.Symbol.ToUpper() == symbol, ct)
                ?? throw ValidationException.For("departmentSymbol",
                    $"Wydział {AssistantWording.Clip(symbol, 16)} nie jest przypisany do tej sprawy.");
            var asked = Date(askedOn, "askedOn");
            var answered = Date(answeredOn, "answeredOn");
            if (asked is null && answered is null)
            {
                throw ValidationException.For("askedOn", "Podaj datę wysłania zapytania albo datę odpowiedzi wydziału (RRRR-MM-DD).");
            }

            var dates = AssistantMapper.ToDepartmentDates(asked ?? link.AskedOn, answered ?? link.AnsweredOn);
            RequestDepartmentService.CheckDepartmentDates(dates, clock.Today);
            if (dates.AskedOn == link.AskedOn && dates.AnsweredOn == link.AnsweredOn)
            {
                throw ValidationException.For("askedOn", "Wydział ma już zapisane te daty.");
            }

            var details = new List<string>
            {
                $"Wydział: {link.Department.Symbol} — {link.Department.Name}",
                $"Data wysłania zapytania: {AssistantWording.Date(dates.AskedOn)}{Previous(link.AskedOn, dates.AskedOn)}",
                $"Data odpowiedzi wydziału: {AssistantWording.Date(dates.AnsweredOn)}{Previous(link.AnsweredOn, dates.AnsweredOn)}",
            };
            if (dates.AskedOn is not null && request.Status == RequestStatus.NoAction)
            {
                details.Add($"Status sprawy zmieni się na {AssistantWording.Quoted(AssistantWording.Status(RequestStatus.QueriesSent))}.");
            }

            return new Proposal(
                AssistantActionKind.UpdateDepartmentDates,
                $"Zapisanie dat dla wydziału {link.Department.Symbol} w sprawie {request.CaseNumber}",
                details,
                new AssistantActionArguments { DepartmentId = link.DepartmentId, AskedOn = asked, AnsweredOn = answered });
        }, ct);

    private async Task<string> ProposeAsync(string? caseNumber, Func<InfoRequest, Task<Proposal>> build, CancellationToken ct)
    {
        if (Proposed.Count >= MaxProposals)
        {
            return $"W jednej odpowiedzi można przygotować najwyżej {MaxProposals} propozycji.";
        }

        var wanted = caseNumber?.Trim().ToUpperInvariant();
        var request = string.IsNullOrEmpty(wanted)
            ? null
            : await db.Requests.AsNoTracking().FirstOrDefaultAsync(r => r.CaseNumber.ToUpper() == wanted, ct);
        if (request is null)
        {
            return ToolArguments.CaseNotFound(caseNumber);
        }

        Proposal proposal;
        try
        {
            proposal = await build(request);
        }
        catch (AppException ex)
        {
            return $"Nie można przygotować tej propozycji: {AssistantWording.Reason(ex)}";
        }

        if (Proposed.Any(p => p.RequestId == request.Id && p.Summary == proposal.Summary && p.Details.SequenceEqual(proposal.Details)))
        {
            return "Taka propozycja została już przygotowana w tej odpowiedzi.";
        }

        Proposed.Add(new AssistantAction
        {
            Id = Guid.CreateVersion7(),
            Kind = proposal.Kind,
            RequestId = request.Id,
            CaseNumber = request.CaseNumber,
            Summary = proposal.Summary,
            Details = proposal.Details,
            Arguments = proposal.Arguments,
            RequestVersion = request.Version,
            Status = AssistantActionStatus.Proposed,
            CreatedAt = clock.Now,
        });
        return $"Przygotowano propozycję: {proposal.Summary}. Zostanie wykonana dopiero wtedy, gdy urzędnik ją sprawdzi " +
               "i zatwierdzi na karcie propozycji w oknie asystenta. Nie pisz, że zmiana została już wprowadzona.";
    }

    private static DateOnly? Date(string? value, string field)
    {
        var parsed = ToolArguments.ParseDate(value);
        return parsed.Invalid ? throw ValidationException.For(field, InvalidDate) : parsed.Value;
    }

    private static List<string> Symbols(string[]? symbols) =>
        (symbols ?? [])
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();

    private static string Previous(DateOnly? before, DateOnly? after) =>
        before == after ? "" : $" (było: {AssistantWording.Date(before)})";

    private sealed record Proposal(
        AssistantActionKind Kind, string Summary, List<string> Details, AssistantActionArguments Arguments);
}
