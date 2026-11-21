namespace ClerkAssistant.Api.Domain;

public enum AssistantRole
{
    User,
    Assistant,
}

public enum AssistantActionKind
{
    ChangeStatus,
    ExtendDeadline,
    SetDepartments,
    UpdateDepartmentDates,
}

public enum AssistantActionStatus
{
    Proposed,
    Executed,
    Rejected,
    Failed,
}

public class AssistantConversation
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public required string Title { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<AssistantMessage> Messages { get; set; } = [];
}

public class AssistantMessage
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public long Sequence { get; set; }
    public AssistantRole Role { get; set; }
    public string Content { get; set; } = "";
    public bool AiUsed { get; set; }
    public bool Interrupted { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public List<AssistantAction> Actions { get; set; } = [];
}

public sealed record AssistantActionArguments
{
    public RequestStatus? Status { get; init; }
    public DateOnly? RespondedOn { get; init; }
    public int? Days { get; init; }
    public string? Reason { get; init; }
    public ExtensionKind? ExtensionKind { get; init; }
    public Guid[]? DepartmentIds { get; init; }
    public Guid[]? PreviousDepartmentIds { get; init; }
    public Guid? DepartmentId { get; init; }
    public DateOnly? AskedOn { get; init; }
    public DateOnly? AnsweredOn { get; init; }
}

public class AssistantAction
{
    public Guid Id { get; set; }
    public Guid MessageId { get; set; }
    public AssistantActionKind Kind { get; set; }
    public Guid RequestId { get; set; }
    public required string CaseNumber { get; set; }
    public required string Summary { get; set; }
    public List<string> Details { get; set; } = [];
    public AssistantActionArguments Arguments { get; set; } = new();
    public uint RequestVersion { get; set; }
    public AssistantActionStatus Status { get; set; }
    public string? Outcome { get; set; }
    public string? Letter { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public uint Version { get; set; }
}
