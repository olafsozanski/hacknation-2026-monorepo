using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Features.Requests;
using ClerkAssistant.Api.Features.Search;
using ClerkAssistant.Api.Infrastructure.Common;

namespace ClerkAssistant.Api.Features.Assistant;

public static class AssistantWording
{
    public static string Status(RequestStatus status) => status switch
    {
        RequestStatus.NoAction => "Brak działań",
        RequestStatus.QueriesSent => "Zapytania wysłane do wydziałów",
        RequestStatus.Extended => "Termin przedłużony",
        RequestStatus.ResponsePrepared => "Odpowiedź przygotowana",
        RequestStatus.ResponseSent => "Odpowiedź wysłana",
        _ => status.ToString(),
    };

    public static string ExtensionKind(ExtensionKind kind) => kind switch
    {
        Domain.ExtensionKind.Query => "kwerenda",
        Domain.ExtensionKind.Clarification => "doprecyzowanie wniosku",
        _ => kind.ToString(),
    };

    public static string Date(DateOnly date) => RequestLetters.ShortDate(date);

    public static string Date(DateOnly? date) => date is { } value ? Date(value) : "—";

    public static string Days(int count) => Math.Abs(count) == 1 ? $"{count} dzień" : $"{count} dni";

    public static string DaysLeft(int daysLeft) => daysLeft switch
    {
        < 0 => $"termin minął {Days(-daysLeft)} temu",
        0 => "termin upływa dziś",
        1 => "termin upływa jutro",
        _ => $"pozostało {Days(daysLeft)}",
    };

    public static string KnowledgeKind(KnowledgeKind? kind) => kind switch
    {
        Domain.KnowledgeKind.Law => "Akt prawny",
        Domain.KnowledgeKind.Ordinance => "Zarządzenie",
        Domain.KnowledgeKind.ArchivedResponse => "Archiwalna odpowiedź",
        Domain.KnowledgeKind.Guideline => "Wytyczne",
        _ => "Dokument z bazy wiedzy",
    };

    public static string Quoted(string text) => $"„{text}”";

    public static string Reason(AppException exception) => exception is ValidationException validation
        ? string.Join(" ", validation.Errors.SelectMany(e => e.Value).Distinct())
        : exception.Message;

    public static string Clip(string text, int maxLength)
    {
        var plain = SearchText.CollapseWhitespace(text);
        if (plain.Length <= maxLength)
        {
            return plain;
        }

        var cut = plain.LastIndexOf(' ', maxLength - 1);
        return plain[..(cut > maxLength / 2 ? cut : maxLength - 1)].TrimEnd() + "…";
    }
}
