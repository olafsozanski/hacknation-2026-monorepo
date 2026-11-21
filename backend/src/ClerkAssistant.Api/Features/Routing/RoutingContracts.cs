namespace ClerkAssistant.Api.Features.Routing;

public enum SuggestionSource
{
    Ai,
    History,
    Keywords,
}

public record DepartmentSuggestion(
    Guid DepartmentId,
    string Symbol,
    string Name,
    string Reason,
    double Confidence,
    int SimilarCaseCount,
    double? AverageResponseDays,
    SuggestionSource Source);
