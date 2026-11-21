using System.ComponentModel.DataAnnotations;
using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Features.Anonymization;

namespace ClerkAssistant.Api.Features.Drafts;

public record DraftDto(
    string Content,
    Redaction[] Redactions,
    bool GeneratedByAi,
    DateTimeOffset UpdatedAt,
    string UpdatedBy,
    uint Version);

public record GenerateDraftResult(DraftDto Draft, string[] Warnings, bool AiUsed);

public record SaveDraftRequest(
    [property: Required, MaxLength(200_000)] string Content,
    [property: Required, MaxLength(RedactionSet.MaxRedactions)] Redaction[] Redactions,
    uint? Version);

public record DetectRedactionsRequest(
    [property: Required, MaxLength(200_000)] string Content,
    [property: Required, MaxLength(RedactionSet.MaxRedactions)] Redaction[] Existing);

public record DetectRedactionsResult(Redaction[] Redactions, string[] Warnings, bool AiUsed);
