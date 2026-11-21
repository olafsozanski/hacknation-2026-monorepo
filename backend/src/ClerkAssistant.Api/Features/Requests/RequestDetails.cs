using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Features.Documents;
using ClerkAssistant.Api.Infrastructure.Common;
using ClerkAssistant.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClerkAssistant.Api.Features.Requests;

public static class RequestDetails
{
    public static async Task<RequestDetail?> LoadAsync(AppDbContext db, Guid id, DateOnly today, CancellationToken ct)
    {
        var request = await db.Requests
            .AsNoTracking()
            .Include(r => r.Assignee)
            .Include(r => r.Departments).ThenInclude(d => d.Department)
            .FirstOrDefaultAsync(r => r.Id == id, ct);
        if (request is null)
        {
            return null;
        }

        var documents = await db.RequestDocuments
            .Where(d => d.RequestId == id)
            .ToDtos(db)
            .ToArrayAsync(ct);
        var draft = await db.Drafts
            .Where(d => d.RequestId == id)
            .Select(d => new { d.UpdatedAt, d.GeneratedByAi, d.Redactions })
            .FirstOrDefaultAsync(ct);

        return new RequestDetail(
            request.Id,
            request.Year,
            request.SequenceNumber,
            request.CaseNumber,
            request.Subject,
            request.Content,
            request.RequesterName,
            request.RequesterContact,
            request.ReplyChannel,
            request.ReceivedOn,
            request.DueOn,
            request.EffectiveDueOn,
            request.ExtendedDueOn,
            request.ExtensionDays,
            request.ExtensionReason,
            Deadlines.LatestExtendedDueDate(request.ReceivedOn),
            request.RespondedOn,
            request.Status,
            Deadlines.UrgencyOf(request, today),
            Deadlines.DaysLeft(request.EffectiveDueOn, today),
            UserSummary.From(request.Assignee),
            request.Departments.OrderBy(d => d.Department.Symbol, StringComparer.Ordinal).Select(d => d.ToDto()).ToArray(),
            documents,
            draft is null
                ? null
                : new DraftSummary(
                    draft.UpdatedAt,
                    draft.GeneratedByAi,
                    draft.Redactions.Count(r => r.Status == RedactionStatus.Suggested),
                    draft.Redactions.Count(r => r.Status == RedactionStatus.Approved)),
            request.Version);
    }

    public static RequestDepartmentDto ToDto(this RequestDepartment link) => new(
        link.DepartmentId,
        link.Department.Symbol,
        link.Department.Name,
        link.Department.Email,
        link.AskedOn,
        link.AnsweredOn);
}
