namespace ClerkAssistant.Api.Domain;

public class AuditEntry
{
    public long Id { get; set; }
    public DateTimeOffset At { get; set; }
    public Guid? UserId { get; set; }
    public required string UserName { get; set; }
    public required string Action { get; set; }
    public required string EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public string? Details { get; set; }
}
