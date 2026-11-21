using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Infrastructure.Common;
using ClerkAssistant.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ClerkAssistant.Api.Features.Requests;

public sealed class RequestDepartmentService(
    AppDbContext db,
    AuditLog audit,
    AppClock clock,
    IOptions<OrganizationOptions> organization)
{
    private const int ReplyBufferWorkingDays = 3;

    public async Task<RequestDepartmentDto[]> SetAsync(Guid id, SetDepartmentsRequest body, CancellationToken ct)
    {
        if (!await db.Requests.AnyAsync(r => r.Id == id, ct))
        {
            throw RequestRules.RequestNotFound();
        }

        var errors = new ValidationErrors();
        var departments = await errors.CheckDepartmentsAsync(db, body.DepartmentIds, ct);
        errors.ThrowIfAny();

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

        var current = await db.RequestDepartments
            .AsNoTracking()
            .Where(l => l.RequestId == id)
            .Include(l => l.Department)
            .ToListAsync(ct);
        return current.ToOrderedDtos();
    }

    public async Task<RequestDepartmentDto> UpdateAsync(
        Guid id, Guid departmentId, UpdateRequestDepartmentRequest body, CancellationToken ct)
    {
        var link = await db.RequestDepartments
            .Include(l => l.Department)
            .FirstOrDefaultAsync(l => l.RequestId == id && l.DepartmentId == departmentId, ct)
            ?? throw new NotFoundException("Wydział nie jest przypisany do tego wniosku.");

        CheckDepartmentDates(body, clock.Today);
        link.AskedOn = body.AskedOn;
        link.AnsweredOn = body.AnsweredOn;
        audit.Record("request.department-updated", nameof(InfoRequest), id,
            AuditDetails.DepartmentDates(link.Department.Symbol, link.AskedOn, link.AnsweredOn));

        if (body.AskedOn is not null)
        {
            await MarkQueriesSentAsync(id, ct);
        }

        await db.SaveChangesAsync(ct);
        return link.ToDto();
    }

    public async Task<DepartmentQueryDto> QueryAsync(Guid id, CancellationToken ct)
    {
        var request = await db.Requests
            .AsNoTracking()
            .Include(r => r.Departments).ThenInclude(l => l.Department)
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw RequestRules.RequestNotFound();

        var departments = request.Departments
            .Select(l => l.Department)
            .OrderBy(d => d.Symbol, StringComparer.Ordinal)
            .ToList();
        var replyBy = ReplyByDate(request.EffectiveDueOn, clock.Today);
        return RequestMapper.ToDepartmentQuery(
            RequestLetters.DepartmentQuerySubject(request, replyBy),
            RequestLetters.DepartmentQueryBody(request, departments, replyBy, organization.Value),
            replyBy);
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

    private static void CheckDepartmentDates(UpdateRequestDepartmentRequest body, DateOnly today)
    {
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

        errors.ThrowIfAny();
    }

    private async Task MarkQueriesSentAsync(Guid id, CancellationToken ct)
    {
        var request = await db.FindRequestAsync(id, ct);
        if (request.Status != RequestStatus.NoAction)
        {
            return;
        }

        request.Status = RequestStatus.QueriesSent;
        request.UpdatedAt = clock.Now;
        audit.Record("request.status-changed", nameof(InfoRequest), id,
            AuditDetails.StatusChange(RequestStatus.NoAction, RequestStatus.QueriesSent));
    }
}
