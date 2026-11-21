using System.ComponentModel.DataAnnotations;
using ClerkAssistant.Api.Domain;

namespace ClerkAssistant.Api.Features.Anonymization;

public record AnalyzeTextRequest([property: Required, MaxLength(500_000)] string Text);

public record AnonymizationResult(
    string? FileName,
    string Text,
    Redaction[] Redactions,
    string[] Warnings,
    bool AiUsed);

public record AnonymizationExportRequest(
    [property: Required, MaxLength(500_000)] string Content,
    [property: Required, MaxLength(RedactionSet.MaxRedactions)] Redaction[] Redactions,
    [property: MaxLength(8)] string? Format,
    [property: MaxLength(255)] string? FileName);
