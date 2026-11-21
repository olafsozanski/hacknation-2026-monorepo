using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Features.Anonymization;
using Riok.Mapperly.Abstractions;

namespace ClerkAssistant.Api.Features.Drafts;

[Mapper]
public static partial class DraftMapper
{
    public static partial DraftDto ToDto(this ResponseDraft draft, string updatedBy);

    [MapPropertyFromSource(nameof(GenerateDraftResult.Draft))]
    public static partial GenerateDraftResult ToGenerateResult(this DraftDto draft, string[] warnings, bool aiUsed);

    [MapProperty(nameof(DetectionOutcome.Suggestions), nameof(DetectRedactionsResult.Redactions))]
    public static partial DetectRedactionsResult ToDetectResult(this DetectionOutcome outcome);
}
