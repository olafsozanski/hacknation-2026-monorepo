using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Infrastructure.Common;
using ClerkAssistant.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClerkAssistant.Api.Features.Requests;

public sealed class RequestRegisterService(AppDbContext db, AppClock clock)
{
    private const string LikeEscape = "\\";

    public async Task<RequestListItem[]> ListAsync(RegisterFilter filter, CancellationToken ct)
    {
        var today = clock.Today;
        var query = Matching(InRegisterOf(filter.Year ?? today.Year), filter.Search);
        if (filter.Status is { } status)
        {
            query = query.Where(r => r.Status == status);
        }

        if (filter.AssigneeId is { } assigneeId)
        {
            query = query.Where(r => r.AssigneeId == assigneeId);
        }

        if (filter.DueOn is { } dueOn)
        {
            query = query.Where(r => (r.ExtendedDueOn ?? r.DueOn) == dueOn);
        }

        var rows = await LoadRowsAsync(query, ct);
        return InRegisterOrder(rows)
            .Select(row => row.Request.ToListItem(
                Deadlines.UrgencyOf(row.Request, today),
                Deadlines.DaysLeft(row.Request.EffectiveDueOn, today),
                row.DepartmentSymbols))
            .ToArray();
    }

    public async Task<RegisterSummary> SummaryAsync(int? year, CancellationToken ct)
    {
        var today = clock.Today;
        var rows = await LoadRowsAsync(InRegisterOf(year ?? today.Year), ct);
        return rows.Select(row => Deadlines.UrgencyOf(row.Request, today)).ToList().ToRegisterSummary();
    }

    public async Task<int[]> YearsAsync(CancellationToken ct)
    {
        var years = await db.Requests.Select(r => r.Year).Distinct().ToListAsync(ct);
        return years.Append(clock.Today.Year).Distinct().OrderDescending().ToArray();
    }

    public static DeadlinePreview PreviewDeadline(DateOnly receivedOn) =>
        RequestMapper.ToDeadlinePreview(Deadlines.InitialDueDate(receivedOn), Deadlines.LatestExtendedDueDate(receivedOn));

    private IQueryable<InfoRequest> InRegisterOf(int year) =>
        db.Requests.Where(r => r.Year == year || (r.Year < year && r.Status != RequestStatus.ResponseSent));

    // Only the columns the register shows: content and the search vector can be large.
    private static Task<List<RegisterRow>> LoadRowsAsync(IQueryable<InfoRequest> requests, CancellationToken ct) =>
        requests
            .AsNoTracking()
            .Select(r => new RegisterRow(
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
                    Assignee = r.Assignee,
                },
                r.Departments.Select(d => d.Department.Symbol).OrderBy(s => s).ToArray()))
            .ToListAsync(ct);

    private static IEnumerable<RegisterRow> InRegisterOrder(IReadOnlyCollection<RegisterRow> rows)
    {
        var open = rows
            .Where(e => !e.Request.IsClosed)
            .OrderBy(e => e.Request.EffectiveDueOn)
            .ThenBy(e => e.Request.Year)
            .ThenBy(e => e.Request.SequenceNumber);
        var closed = rows
            .Where(e => e.Request.IsClosed)
            .OrderByDescending(e => e.Request.RespondedOn)
            .ThenByDescending(e => e.Request.Year)
            .ThenByDescending(e => e.Request.SequenceNumber);
        return open.Concat(closed);
    }

    private static IQueryable<InfoRequest> Matching(IQueryable<InfoRequest> requests, string? search)
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

    private static string EscapeLike(string value) =>
        value.Replace(LikeEscape, LikeEscape + LikeEscape).Replace("%", LikeEscape + "%").Replace("_", LikeEscape + "_");

    private sealed record RegisterRow(InfoRequest Request, string[] DepartmentSymbols);
}
