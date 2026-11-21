using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Infrastructure.Common;
using ClerkAssistant.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ClerkAssistant.Api.Features.Requests;

public static class RequestDepartmentLinks
{
    private const int ReplyBufferWorkingDays = 3;

    public static async Task<Results<Ok<RequestDepartmentDto[]>, NotFound, ValidationProblem>> Set(
        Guid id, SetDepartmentsRequest body, AppDbContext db, AuditLog audit, CancellationToken ct)
    {
        if (!await db.Requests.AnyAsync(r => r.Id == id, ct))
        {
            return TypedResults.NotFound();
        }

        var errors = new ValidationErrors();
        var departments = await errors.CheckDepartmentsAsync(db, body.DepartmentIds, ct);
        if (errors.Any)
        {
            return errors.ToProblem();
        }

        var wanted = departments.Select(d => d.Id).ToHashSet();
        var links = await db.RequestDepartments.Where(l => l.RequestId == id).ToListAsync(ct);
        var removed = links.Where(l => !wanted.Contains(l.DepartmentId)).ToList();
        var added = wanted.Except(links.Select(l => l.DepartmentId)).ToList();
        if (removed.Count > 0 || added.Count > 0)
        {
            db.RequestDepartments.RemoveRange(removed);
            db.RequestDepartments.AddRange(added.Select(departmentId => new RequestDepartment
            {
                RequestId = id,
                DepartmentId = departmentId,
            }));
            audit.Record("request.departments-changed", nameof(InfoRequest), id,
                AuditDetails.Departments(departments.Select(d => d.Symbol)));
            await db.SaveChangesAsync(ct);
        }

        var result = await db.RequestDepartments
            .AsNoTracking()
            .Where(l => l.RequestId == id)
            .Include(l => l.Department)
            .OrderBy(l => l.Department.Symbol)
            .ToListAsync(ct);
        return TypedResults.Ok(result.Select(l => l.ToDto()).ToArray());
    }

    public static async Task<Results<Ok<RequestDepartmentDto>, NotFound, ValidationProblem>> Update(
        Guid id,
        Guid departmentId,
        UpdateRequestDepartmentRequest body,
        AppDbContext db,
        AuditLog audit,
        AppClock clock,
        CancellationToken ct)
    {
        var link = await db.RequestDepartments
            .Include(l => l.Department)
            .FirstOrDefaultAsync(l => l.RequestId == id && l.DepartmentId == departmentId, ct);
        if (link is null)
        {
            return TypedResults.NotFound();
        }

        var today = clock.Today;
        var errors = new ValidationErrors();
        if (body.AskedOn > today)
        {
            errors.Add("askedOn", "Data wysłania zapytania nie może być późniejsza niż dzisiejsza.");
        }

        if (body.AnsweredOn > today)
        {
            errors.Add("answeredOn", "Data odpowiedzi wydziału nie może być późniejsza niż dzisiejsza.");
        }
        else if (body.AnsweredOn < body.AskedOn)
        {
            errors.Add("answeredOn", "Data odpowiedzi wydziału nie może być wcześniejsza niż data wysłania zapytania.");
        }

        if (errors.Any)
        {
            return errors.ToProblem();
        }

        link.AskedOn = body.AskedOn;
        link.AnsweredOn = body.AnsweredOn;
        audit.Record("request.department-updated", nameof(InfoRequest), id,
            AuditDetails.DepartmentDates(link.Department.Symbol, link.AskedOn, link.AnsweredOn));

        if (body.AskedOn is not null)
        {
            var request = await db.Requests.FirstAsync(r => r.Id == id, ct);
            if (request.Status == RequestStatus.NoAction)
            {
                request.Status = RequestStatus.QueriesSent;
                request.UpdatedAt = clock.Now;
                audit.Record("request.status-changed", nameof(InfoRequest), id,
                    AuditDetails.StatusChange(RequestStatus.NoAction, RequestStatus.QueriesSent));
            }
        }

        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(link.ToDto());
    }

    public static async Task<Results<Ok<DepartmentQueryDto>, NotFound>> Query(
        Guid id, AppDbContext db, AppClock clock, IOptions<OrganizationOptions> organization, CancellationToken ct)
    {
        var request = await db.Requests
            .AsNoTracking()
            .Include(r => r.Departments).ThenInclude(l => l.Department)
            .FirstOrDefaultAsync(r => r.Id == id, ct);
        if (request is null)
        {
            return TypedResults.NotFound();
        }

        var departments = request.Departments
            .Select(l => l.Department)
            .OrderBy(d => d.Symbol, StringComparer.Ordinal)
            .ToList();
        var replyBy = ReplyByDate(request.EffectiveDueOn, clock.Today);
        return TypedResults.Ok(RequestLetters.DepartmentQuery(request, departments, replyBy, organization.Value));
    }

    public static DateOnly ReplyByDate(DateOnly effectiveDueOn, DateOnly today)
    {
        var date = effectiveDueOn;
        for (var remaining = ReplyBufferWorkingDays; remaining > 0;)
        {
            date = date.AddDays(-1);
            if (!PolishHolidays.IsNonWorkingDay(date))
            {
                remaining--;
            }
        }

        return date < today ? today : date;
    }
}
