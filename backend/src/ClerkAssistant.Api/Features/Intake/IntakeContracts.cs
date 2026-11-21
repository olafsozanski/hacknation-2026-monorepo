using System.ComponentModel.DataAnnotations;
using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Features.Routing;
using ClerkAssistant.Api.Features.Search;

namespace ClerkAssistant.Api.Features.Intake;

public record IntakeTextRequest([property: Required, MaxLength(500_000)] string Text);

public record IntakeSuggestion(
    string? Subject,
    string? RequesterName,
    string? RequesterContact,
    ReplyChannel? ReplyChannel,
    DateOnly? RequestDate,
    string[] RequestedItems,
    bool LooksLikePublicInformationRequest,
    string? Remarks);

public record IntakeResult(
    Guid? FileId,
    string? FileName,
    string Text,
    IntakeSuggestion Suggestion,
    string SuggestedCaseNumber,
    DateOnly SuggestedReceivedOn,
    DateOnly SuggestedDueOn,
    SearchHit[] SimilarCases,
    DepartmentSuggestion[] DepartmentSuggestions,
    string[] Warnings,
    bool AiUsed);
