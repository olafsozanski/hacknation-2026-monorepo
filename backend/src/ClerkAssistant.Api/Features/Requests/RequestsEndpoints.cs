using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Infrastructure.Common;
using ClerkAssistant.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace ClerkAssistant.Api.Features.Requests;

public static class RequestsEndpoints
{
    public static IServiceCollection AddRequestsFeature(this IServiceCollection services) =>
        services.AddScoped<CaseNumberGenerator>();

    public static void MapRequestsEndpoints(this RouteGroupBuilder api)
    {
        var requests = api.MapGroup("/requests");
        requests.MapGet("/", List);
        requests.MapGet("/summary", Summary);
        requests.MapGet("/years", Years);
        requests.MapGet("/deadline-preview", PreviewDeadline);
        requests.MapGet("/{id:guid}", Get);
        requests.MapPost("/", RequestCommands.Create);
        requests.MapPut("/{id:guid}", RequestCommands.Update);
        requests.MapPost("/{id:guid}/status", RequestCommands.ChangeStatus);
        requests.MapPost("/{id:guid}/extension", RequestCommands.Extend);
        requests.MapPut("/{id:guid}/departments", RequestDepartmentLinks.Set);
        requests.MapPatch("/{id:guid}/departments/{departmentId:guid}", RequestDepartmentLinks.Update);
        requests.MapGet("/{id:guid}/department-query", RequestDepartmentLinks.Query);
        requests.MapGet("/{id:guid}/history", History);
    }

    private static async Task<RequestListItem[]> List(
        int? year,
        RequestStatus? status,
        Guid? assigneeId,
        string? search,
        DateOnly? dueOn,
        AppDbContext db,
        AppClock clock,
        CancellationToken ct)
    {
        var today = clock.Today;
        var query = db.Requests.InRegisterOf(year ?? today.Year).Matching(search);
        if (status is { } s)
        {
            query = query.Where(r => r.Status == s);
        }

        if (assigneeId is { } a)
        {
            query = query.Where(r => r.AssigneeId == a);
        }

        if (dueOn is { } d)
        {
            query = query.Where(r => (r.ExtendedDueOn ?? r.DueOn) == d);
        }

        var entries = await query.ToEntriesAsync(ct);
        return entries.InRegisterOrder().Select(e => e.ToListItem(today)).ToArray();
    }

    private static async Task<RegisterSummary> Summary(int? year, AppDbContext db, AppClock clock, CancellationToken ct)
    {
        var today = clock.Today;
        var entries = await db.Requests.InRegisterOf(year ?? today.Year).ToEntriesAsync(ct);
        return entries.Summarize(today);
    }

    private static async Task<int[]> Years(AppDbContext db, AppClock clock, CancellationToken ct)
    {
        var years = await db.Requests.Select(r => r.Year).Distinct().ToListAsync(ct);
        return years.Append(clock.Today.Year).Distinct().OrderDescending().ToArray();
    }

    private static DeadlinePreview PreviewDeadline(DateOnly receivedOn) =>
        new(Deadlines.InitialDueDate(receivedOn), Deadlines.LatestExtendedDueDate(receivedOn));

    private static async Task<Results<Ok<RequestDetail>, NotFound>> Get(
        Guid id, AppDbContext db, AppClock clock, CancellationToken ct) =>
        await RequestDetails.LoadAsync(db, id, clock.Today, ct) is { } detail
            ? TypedResults.Ok(detail)
            : TypedResults.NotFound();

    private static async Task<HistoryItem[]> History(Guid id, AppDbContext db, CancellationToken ct) =>
        await db.AuditEntries
            .Where(e => e.EntityType == nameof(InfoRequest) && e.EntityId == id)
            .OrderByDescending(e => e.At)
            .ThenByDescending(e => e.Id)
            .Select(e => new HistoryItem(
                e.At,
                db.Users.Where(u => u.Id == e.UserId).Select(u => u.FullName).FirstOrDefault() ?? e.UserName,
                e.Action,
                e.Details))
            .ToArrayAsync(ct);
}
