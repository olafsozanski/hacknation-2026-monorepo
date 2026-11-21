using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Infrastructure.Common;
using Microsoft.EntityFrameworkCore;

namespace ClerkAssistant.Api.Features.Requests;

public sealed record RegisterEntry(InfoRequest Request, UserSummary? Assignee, string[] DepartmentSymbols);

public static class RequestRegister
{
    private const string LikeEscape = "\\";

    public static IQueryable<InfoRequest> InRegisterOf(this IQueryable<InfoRequest> requests, int year) =>
        requests.Where(r => r.Year == year || (r.Year < year && r.Status != RequestStatus.ResponseSent));

    public static IQueryable<InfoRequest> Matching(this IQueryable<InfoRequest> requests, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return requests;
        }

        var pattern = $"%{EscapeLike(search.Trim())}%";
        return requests.Where(r =>
            EF.Functions.ILike(r.CaseNumber, pattern, LikeEscape)
            || EF.Functions.ILike(r.Subject, pattern, LikeEscape)
            || (r.RequesterName != null && EF.Functions.ILike(r.RequesterName, pattern, LikeEscape)));
    }

    public static Task<List<RegisterEntry>> ToEntriesAsync(this IQueryable<InfoRequest> requests, CancellationToken ct) =>
        requests
            .Select(r => new RegisterEntry(
                new InfoRequest
                {
                    Id = r.Id,
                    Year = r.Year,
                    SequenceNumber = r.SequenceNumber,
                    CaseNumber = r.CaseNumber,
                    Subject = r.Subject,
                    RequesterName = r.RequesterName,
                    ReceivedOn = r.ReceivedOn,
                    DueOn = r.DueOn,
                    ExtendedDueOn = r.ExtendedDueOn,
                    ExtensionDays = r.ExtensionDays,
                    ExtensionKind = r.ExtensionKind,
                    RespondedOn = r.RespondedOn,
                    Status = r.Status,
                },
                r.Assignee == null ? null : new UserSummary(r.Assignee.Id, r.Assignee.FullName, r.Assignee.Initials),
                r.Departments.Select(d => d.Department.Symbol).OrderBy(s => s).ToArray()))
            .ToListAsync(ct);

    public static IEnumerable<RegisterEntry> InRegisterOrder(this IReadOnlyCollection<RegisterEntry> entries)
    {
        var open = entries
            .Where(e => !e.Request.IsClosed)
            .OrderBy(e => e.Request.EffectiveDueOn)
            .ThenBy(e => e.Request.Year)
            .ThenBy(e => e.Request.SequenceNumber);
        var closed = entries
            .Where(e => e.Request.IsClosed)
            .OrderByDescending(e => e.Request.RespondedOn)
            .ThenByDescending(e => e.Request.Year)
            .ThenByDescending(e => e.Request.SequenceNumber);
        return open.Concat(closed);
    }

    public static RequestListItem ToListItem(this RegisterEntry entry, DateOnly today)
    {
        var r = entry.Request;
        return new RequestListItem(
            r.Id,
            r.SequenceNumber,
            r.CaseNumber,
            r.Subject,
            r.RequesterName,
            r.ReceivedOn,
            r.DueOn,
            r.EffectiveDueOn,
            r.ExtensionDays,
            r.ExtensionKind,
            r.RespondedOn,
            r.Status,
            Deadlines.UrgencyOf(r, today),
            Deadlines.DaysLeft(r.EffectiveDueOn, today),
            entry.Assignee,
            entry.DepartmentSymbols);
    }

    public static RegisterSummary Summarize(this IReadOnlyCollection<RegisterEntry> entries, DateOnly today)
    {
        var counts = entries
            .GroupBy(e => Deadlines.UrgencyOf(e.Request, today))
            .ToDictionary(g => g.Key, g => g.Count());
        int Count(Urgency urgency) => counts.GetValueOrDefault(urgency);

        var closed = Count(Urgency.Closed);
        return new RegisterSummary(
            Count(Urgency.Overdue),
            Count(Urgency.DueToday),
            Count(Urgency.DueTomorrow),
            Count(Urgency.DueSoon),
            entries.Count - closed,
            closed,
            entries.Count);
    }

    private static string EscapeLike(string value) =>
        value.Replace(LikeEscape, LikeEscape + LikeEscape).Replace("%", LikeEscape + "%").Replace("_", LikeEscape + "_");
}
