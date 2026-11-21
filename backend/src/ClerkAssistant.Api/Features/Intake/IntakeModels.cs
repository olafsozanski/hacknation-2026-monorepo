using ClerkAssistant.Api.Domain;

namespace ClerkAssistant.Api.Features.Intake;

public sealed record RequestExtraction(
    string? Subject,
    string? RequesterName,
    string? RequesterContact,
    ReplyChannel? ReplyChannel,
    DateOnly? RequestDate,
    string[] RequestedItems,
    bool LooksLikePublicInformationRequest,
    string? Remarks)
{
    public static RequestExtraction Empty { get; } = new(null, null, null, null, null, [], false, null);
}

internal sealed record IntakeExtraction(
    string? Subject,
    string? RequesterName,
    string? RequesterContact,
    string? ReplyChannel,
    string? RequestDate,
    List<string>? RequestedItems,
    bool IsPublicInformationRequest,
    string? Remarks);
