using Riok.Mapperly.Abstractions;

namespace ClerkAssistant.Api.Features.Anonymization;

[Mapper]
public static partial class AnonymizationMapper
{
    [MapProperty(nameof(DetectionOutcome.Suggestions), nameof(AnonymizationResult.Redactions))]
    public static partial AnonymizationResult ToResult(this DetectionOutcome outcome, string? fileName, string text);
}
