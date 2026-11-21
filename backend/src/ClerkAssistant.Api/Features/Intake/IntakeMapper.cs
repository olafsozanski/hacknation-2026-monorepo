using ClerkAssistant.Api.Features.Routing;
using ClerkAssistant.Api.Features.Search;
using Riok.Mapperly.Abstractions;

namespace ClerkAssistant.Api.Features.Intake;

[Mapper]
public static partial class IntakeMapper
{
    public static partial IntakeSuggestion ToSuggestion(this RequestExtraction extraction);

    [MapPropertyFromSource(nameof(IntakeResult.Suggestion), Use = nameof(ToSuggestion))]
    public static partial IntakeResult ToResult(
        this RequestExtraction extraction,
        Guid? fileId,
        string? fileName,
        string text,
        string suggestedCaseNumber,
        DateOnly suggestedReceivedOn,
        DateOnly suggestedDueOn,
        SearchHit[] similarCases,
        DepartmentSuggestion[] departmentSuggestions,
        string[] warnings,
        bool aiUsed);
}
