using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Features.Documents;
using ClerkAssistant.Api.Infrastructure.Common;
using Riok.Mapperly.Abstractions;

namespace ClerkAssistant.Api.Features.Requests;

[Mapper]
[UseStaticMapper(typeof(UserMapper))]
public static partial class RequestMapper
{
    public static partial RequestListItem ToListItem(this InfoRequest request, Urgency urgency, int daysLeft, string[] departmentSymbols);

    [MapperIgnoreSource(nameof(InfoRequest.Documents))]
    [MapProperty(nameof(InfoRequest.Departments), nameof(RequestDetail.Departments), Use = nameof(ToOrderedDtos))]
    public static partial RequestDetail ToDetail(
        this InfoRequest request, DateOnly latestExtendedDueOn, Urgency urgency, int daysLeft, DocumentDto[] documents);

    [MapProperty([nameof(RequestDepartment.Department), nameof(Department.Symbol)], nameof(RequestDepartmentDto.Symbol))]
    [MapProperty([nameof(RequestDepartment.Department), nameof(Department.Name)], nameof(RequestDepartmentDto.Name))]
    [MapProperty([nameof(RequestDepartment.Department), nameof(Department.Email)], nameof(RequestDepartmentDto.Email))]
    public static partial RequestDepartmentDto ToDto(this RequestDepartment link);

    [MapProperty(nameof(ResponseDraft.Redactions), nameof(DraftSummary.SuggestedRedactions), Use = nameof(CountSuggested))]
    [MapProperty(nameof(ResponseDraft.Redactions), nameof(DraftSummary.ApprovedRedactions), Use = nameof(CountApproved))]
    public static partial DraftSummary ToSummary(this ResponseDraft draft);

    [MapperIgnoreSource(nameof(AuditEntry.UserName))]
    public static partial HistoryItem ToHistoryItem(this AuditEntry entry, string userName);

    public static partial CreatedResponse ToCreatedResponse(this InfoRequest request);

    public static RequestDepartmentDto[] ToOrderedDtos(this IEnumerable<RequestDepartment> links) =>
        links.OrderBy(l => l.Department.Symbol, StringComparer.Ordinal).Select(l => l.ToDto()).ToArray();

    public static RegisterSummary ToRegisterSummary(this IReadOnlyCollection<Urgency> urgencies)
    {
        var counts = urgencies.GroupBy(u => u).ToDictionary(g => g.Key, g => g.Count());
        int Count(Urgency urgency) => counts.GetValueOrDefault(urgency);

        var closed = Count(Urgency.Closed);
        return new RegisterSummary(
            Count(Urgency.Overdue),
            Count(Urgency.DueToday),
            Count(Urgency.DueTomorrow),
            Count(Urgency.DueSoon),
            urgencies.Count - closed,
            closed,
            urgencies.Count);
    }

    public static DeadlinePreview ToDeadlinePreview(DateOnly dueOn, DateOnly latestExtendedDueOn) => new(dueOn, latestExtendedDueOn);

    public static ExtensionResult ToExtensionResult(DateOnly extendedDueOn, string noticeText) => new(extendedDueOn, noticeText);

    public static DepartmentQueryDto ToDepartmentQuery(string subject, string body, DateOnly replyBy) => new(subject, body, replyBy);

    private static int CountSuggested(List<Redaction> redactions) => redactions.Count(r => r.Status == RedactionStatus.Suggested);

    private static int CountApproved(List<Redaction> redactions) => redactions.Count(r => r.Status == RedactionStatus.Approved);
}
