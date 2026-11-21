namespace ClerkAssistant.Api.Features.Audit;

public record AuditEntryDto(
    long Id,
    DateTimeOffset At,
    string UserName,
    string Action,
    string EntityType,
    Guid? EntityId,
    string? Details);
