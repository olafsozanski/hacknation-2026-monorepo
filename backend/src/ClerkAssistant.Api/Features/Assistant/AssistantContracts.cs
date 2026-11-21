using System.ComponentModel.DataAnnotations;
using ClerkAssistant.Api.Domain;

namespace ClerkAssistant.Api.Features.Assistant;

public record AssistantConversationSummary(Guid Id, string Title, DateTimeOffset UpdatedAt);

public record AssistantConversationDto(Guid Id, string Title, DateTimeOffset UpdatedAt, AssistantMessageDto[] Messages);

public record AssistantMessageDto(
    Guid Id,
    AssistantRole Role,
    string Content,
    DateTimeOffset CreatedAt,
    bool AiUsed,
    bool Interrupted,
    AssistantActionDto[] Actions);

public record AssistantActionDto(
    Guid Id,
    AssistantActionKind Kind,
    Guid RequestId,
    string CaseNumber,
    string Summary,
    string[] Details,
    AssistantActionStatus Status,
    string? Outcome,
    string? Letter,
    DateTimeOffset CreatedAt);

public record SendAssistantMessage(
    Guid? ConversationId,
    [property: Required(ErrorMessage = AssistantLimits.EmptyText),
               MaxLength(AssistantLimits.MaxTextLength, ErrorMessage = AssistantLimits.TextTooLong)] string Text,
    [property: Required(ErrorMessage = AssistantLimits.MissingScreen)] ScreenContext Screen);

public record ScreenContext(
    [property: Required(ErrorMessage = AssistantLimits.MissingPath),
               MaxLength(AssistantLimits.MaxScreenText, ErrorMessage = AssistantLimits.ValueTooLong)] string Path,
    [property: MaxLength(AssistantLimits.MaxScreenText, ErrorMessage = AssistantLimits.ValueTooLong)] string? Title,
    [property: MaxLength(AssistantLimits.MaxScreenText, ErrorMessage = AssistantLimits.ValueTooLong)] string? Heading,
    [property: MaxLength(AssistantLimits.MaxScreenText, ErrorMessage = AssistantLimits.ValueTooLong)] string? Dialog,
    [property: MaxLength(AssistantLimits.MaxFieldErrors, ErrorMessage = AssistantLimits.TooManyItems)] ScreenFieldError[]? FieldErrors,
    [property: MaxLength(AssistantLimits.MaxAlerts, ErrorMessage = AssistantLimits.TooManyItems)] string[]? Alerts);

public record ScreenFieldError(string? Label, string? Message);

public record AssistantConversationStarted(Guid ConversationId, string Title, AssistantMessageDto UserMessage);

public record AssistantText(string Text);

public record AssistantDone(AssistantMessageDto Message, string[] Warnings);

public record AssistantError(string Message, AssistantMessageDto? AssistantMessage);

public static class AssistantLimits
{
    public const int MaxTextLength = 4000;
    public const int MaxScreenText = 300;
    public const int MaxFieldErrors = 20;
    public const int MaxAlerts = 10;
    public const int MaxAlertLength = 500;

    public const string EmptyText = "Wpisz treść wiadomości.";
    public const string TextTooLong = "Wiadomość może mieć najwyżej {1} znaków.";
    public const string MissingScreen = "Brakuje informacji o bieżącym ekranie.";
    public const string MissingPath = "Brakuje adresu bieżącego ekranu.";
    public const string ValueTooLong = "Wartość może mieć najwyżej {1} znaków.";
    public const string TooManyItems = "Lista może mieć najwyżej {1} elementów.";
}
