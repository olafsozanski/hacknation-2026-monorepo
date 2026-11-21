using NpgsqlTypes;
using Pgvector;

namespace ClerkAssistant.Api.Domain;

public enum RequestStatus
{
    NoAction,
    QueriesSent,
    Extended,
    ResponsePrepared,
    ResponseSent,
}

public enum ExtensionKind
{
    Query,
    Clarification,
}

public enum ReplyChannel
{
    Email,
    Post,
    Epuap,
    InPerson,
    Other,
}

public class InfoRequest
{
    public Guid Id { get; set; }
    public int Year { get; set; }
    public int SequenceNumber { get; set; }
    public required string CaseNumber { get; set; }
    public required string Subject { get; set; }
    public string Content { get; set; } = "";
    public string? RequesterName { get; set; }
    public string? RequesterContact { get; set; }
    public ReplyChannel ReplyChannel { get; set; }
    public DateOnly ReceivedOn { get; set; }
    public DateOnly DueOn { get; set; }
    public DateOnly? ExtendedDueOn { get; set; }
    public int? ExtensionDays { get; set; }
    public ExtensionKind? ExtensionKind { get; set; }
    public string? ExtensionReason { get; set; }
    public DateOnly? RespondedOn { get; set; }
    public RequestStatus Status { get; set; }
    public Guid? AssigneeId { get; set; }
    public AppUser? Assignee { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public uint Version { get; set; }
    public NpgsqlTsVector SearchVector { get; set; } = null!;
    public RequestEmbedding? Embedding { get; set; }

    public List<RequestDepartment> Departments { get; set; } = [];
    public List<RequestDocument> Documents { get; set; } = [];
    public ResponseDraft? Draft { get; set; }

    public DateOnly EffectiveDueOn => ExtendedDueOn ?? DueOn;
    public bool IsClosed => Status == RequestStatus.ResponseSent;
}

public class RequestEmbedding
{
    public Guid RequestId { get; set; }
    public required Vector Vector { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class RequestDepartment
{
    public Guid RequestId { get; set; }
    public Guid DepartmentId { get; set; }
    public Department Department { get; set; } = null!;
    public DateOnly? AskedOn { get; set; }
    public DateOnly? AnsweredOn { get; set; }
}
