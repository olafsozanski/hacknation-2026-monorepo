using System.ComponentModel.DataAnnotations;
using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Features.Documents;
using ClerkAssistant.Api.Infrastructure.Common;

namespace ClerkAssistant.Api.Features.Requests;

public record RequestListItem(
    Guid Id,
    int SequenceNumber,
    string CaseNumber,
    string Subject,
    string? RequesterName,
    DateOnly ReceivedOn,
    DateOnly DueOn,
    DateOnly EffectiveDueOn,
    int? ExtensionDays,
    ExtensionKind? ExtensionKind,
    DateOnly? RespondedOn,
    RequestStatus Status,
    Urgency Urgency,
    int DaysLeft,
    UserSummary? Assignee,
    string[] DepartmentSymbols);

public record RegisterFilter(int? Year, RequestStatus? Status, Guid? AssigneeId, string? Search, DateOnly? DueOn);

public record RegisterSummary(
    int Overdue,
    int DueToday,
    int DueTomorrow,
    int DueSoon,
    int Open,
    int Closed,
    int Total);

public record DeadlinePreview(DateOnly DueOn, DateOnly LatestExtendedDueOn);

public record RequestDepartmentDto(
    Guid DepartmentId,
    string Symbol,
    string Name,
    string? Email,
    DateOnly? AskedOn,
    DateOnly? AnsweredOn);

public record DraftSummary(
    DateTimeOffset UpdatedAt,
    bool GeneratedByAi,
    int SuggestedRedactions,
    int ApprovedRedactions,
    uint Version);

public record RequestDetail(
    Guid Id,
    int Year,
    int SequenceNumber,
    string CaseNumber,
    string Subject,
    string Content,
    string? RequesterName,
    string? RequesterContact,
    ReplyChannel ReplyChannel,
    DateOnly ReceivedOn,
    DateOnly DueOn,
    DateOnly EffectiveDueOn,
    DateOnly? ExtendedDueOn,
    int? ExtensionDays,
    ExtensionKind? ExtensionKind,
    string? ExtensionReason,
    DateOnly LatestExtendedDueOn,
    DateOnly? RespondedOn,
    RequestStatus Status,
    Urgency Urgency,
    int DaysLeft,
    UserSummary? Assignee,
    RequestDepartmentDto[] Departments,
    DocumentDto[] Documents,
    DraftSummary? Draft,
    uint Version);

public record CreateRequestRequest(
    [property: Required, MaxLength(500)] string Subject,
    [property: MaxLength(500_000)] string? Content,
    [property: MaxLength(300)] string? RequesterName,
    [property: MaxLength(500)] string? RequesterContact,
    ReplyChannel ReplyChannel,
    DateOnly ReceivedOn,
    DateOnly? DueOn,
    [property: MaxLength(64)] string? CaseNumber,
    Guid? AssigneeId,
    Guid? SourceFileId,
    Guid[]? DepartmentIds);

public record CreatedResponse(Guid Id);

public record UpdateRequestRequest(
    [property: Required, MaxLength(500)] string Subject,
    [property: MaxLength(500_000)] string? Content,
    [property: MaxLength(300)] string? RequesterName,
    [property: MaxLength(500)] string? RequesterContact,
    ReplyChannel ReplyChannel,
    DateOnly ReceivedOn,
    DateOnly DueOn,
    [property: Required, MaxLength(64)] string CaseNumber,
    Guid? AssigneeId,
    uint Version);

public record ChangeStatusRequest(RequestStatus Status, DateOnly? RespondedOn, uint Version);

public record ExtendDeadlineRequest(
    [property: Range(1, 62)] int Days,
    [property: Required, MaxLength(2000)] string Reason,
    uint Version,
    ExtensionKind Kind = ExtensionKind.Query);

public record ExtensionResult(DateOnly ExtendedDueOn, string NoticeText);

public record SetDepartmentsRequest([property: Required] Guid[] DepartmentIds);

public record UpdateRequestDepartmentRequest(DateOnly? AskedOn, DateOnly? AnsweredOn);

public record DepartmentQueryDto(string Subject, string Body, DateOnly ReplyBy);

public record HistoryItem(DateTimeOffset At, string UserName, string Action, string? Details);
