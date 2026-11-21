using ClerkAssistant.Api.Domain;

namespace ClerkAssistant.Api.Features.Requests;

public static class AuditDetails
{
    public static string StatusLabel(RequestStatus status) => status switch
    {
        RequestStatus.NoAction => "bez działań",
        RequestStatus.QueriesSent => "wysłano zapytania",
        RequestStatus.Extended => "przedłużony termin",
        RequestStatus.ResponsePrepared => "odpowiedź przygotowana",
        RequestStatus.ResponseSent => "odpowiedź wysłana",
        _ => status.ToString(),
    };

    public static string ExtensionKindLabel(ExtensionKind kind) => kind switch
    {
        ExtensionKind.Query => "kwerenda",
        ExtensionKind.Clarification => "wezwanie do doprecyzowania wniosku",
        _ => kind.ToString(),
    };

    public static string Extension(ExtensionKind kind, DateOnly extendedDueOn, int days) =>
        $"Przedłużenie terminu ({ExtensionKindLabel(kind)}): nowy termin {RequestLetters.ShortDate(extendedDueOn)} (+{days} dni)";

    public static string StatusChange(RequestStatus from, RequestStatus to) =>
        $"Status: {StatusLabel(from)} → {StatusLabel(to)}";

    public static string Departments(IEnumerable<string> symbols)
    {
        var list = string.Join(", ", symbols);
        return list.Length == 0 ? "Wydziały: brak" : $"Wydziały: {list}";
    }

    public static string DepartmentDates(string symbol, DateOnly? askedOn, DateOnly? answeredOn) =>
        $"{symbol}: zapytanie {Date(askedOn)}, odpowiedź {Date(answeredOn)}";

    public static string ChangedFields(IEnumerable<string> fields) => "Zmieniono: " + string.Join(", ", fields);

    private static string Date(DateOnly? date) => date is { } d ? RequestLetters.ShortDate(d) : "—";
}
