using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Infrastructure.Common;
using ClerkAssistant.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ClerkAssistant.Api.Features.Requests;

public static class RequestCommands
{
    public static async Task<Results<Created<CreatedResponse>, ValidationProblem>> Create(
        CreateRequestRequest body,
        AppDbContext db,
        CaseNumberGenerator numbers,
        CurrentUser current,
        AuditLog audit,
        AppClock clock,
        CancellationToken ct)
    {
        var dueOn = body.DueOn ?? Deadlines.InitialDueDate(body.ReceivedOn);
        var caseNumber = RequestRules.Clean(body.CaseNumber);
        var errors = new ValidationErrors();
        errors.CheckDates(body.ReceivedOn, dueOn, clock.Today);
        if (caseNumber is not null)
        {
            await errors.CheckCaseNumberAsync(db, caseNumber, null, ct);
        }

        if (body.AssigneeId is { } assigneeId)
        {
            await errors.CheckAssigneeAsync(db, assigneeId, ct);
        }

        var departments = await errors.CheckDepartmentsAsync(db, body.DepartmentIds ?? [], ct);
        var sourceText = body.SourceFileId is { } fileId ? await errors.CheckSourceFileAsync(db, fileId, ct) : null;
        if (errors.Any)
        {
            return errors.ToProblem();
        }

        var content = string.IsNullOrWhiteSpace(body.Content) ? sourceText?.Trim() ?? "" : body.Content.Trim();
        for (var attempt = 1; ; attempt++)
        {
            var (sequence, generatedNumber) = await AllocateNumberAsync(db, numbers, body.ReceivedOn.Year, ct);
            var request = new InfoRequest
            {
                Id = Guid.CreateVersion7(),
                Year = body.ReceivedOn.Year,
                SequenceNumber = sequence,
                CaseNumber = caseNumber ?? generatedNumber,
                Subject = body.Subject.Trim(),
                Content = content,
                RequesterName = RequestRules.Clean(body.RequesterName),
                RequesterContact = RequestRules.Clean(body.RequesterContact),
                ReplyChannel = body.ReplyChannel,
                ReceivedOn = body.ReceivedOn,
                DueOn = dueOn,
                Status = RequestStatus.NoAction,
                AssigneeId = body.AssigneeId ?? current.Id,
                CreatedAt = clock.Now,
                UpdatedAt = clock.Now,
                Departments = departments.Select(d => new RequestDepartment { DepartmentId = d.Id }).ToList(),
            };
            if (body.SourceFileId is { } sourceFileId)
            {
                request.Documents.Add(new RequestDocument
                {
                    Id = Guid.CreateVersion7(),
                    Kind = DocumentKind.Request,
                    FileId = sourceFileId,
                    UploadedAt = clock.Now,
                    UploadedById = current.Id,
                });
            }

            db.Requests.Add(request);
            audit.Record("request.created", nameof(InfoRequest), request.Id, request.CaseNumber);
            try
            {
                await db.SaveChangesAsync(ct);
                return TypedResults.Created($"/api/requests/{request.Id}", new CreatedResponse(request.Id));
            }
            catch (DbUpdateException ex) when (ex.IsUniqueViolation())
            {
                db.ChangeTracker.Clear();
                if (caseNumber is not null && ex.IsUniqueViolation(RequestRules.CaseNumberIndex))
                {
                    return ValidationErrors.Single("caseNumber", RequestRules.DuplicateCaseNumber(caseNumber));
                }

                if (attempt > 1)
                {
                    throw;
                }
            }
        }
    }

    public static async Task<Results<Ok<RequestDetail>, NotFound, ValidationProblem>> Update(
        Guid id, UpdateRequestRequest body, AppDbContext db, AuditLog audit, AppClock clock, CancellationToken ct)
    {
        var request = await db.Requests.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (request is null)
        {
            return TypedResults.NotFound();
        }

        var caseNumber = body.CaseNumber.Trim();
        var errors = new ValidationErrors();
        errors.CheckDates(body.ReceivedOn, body.DueOn, clock.Today);
        if (caseNumber != request.CaseNumber)
        {
            await errors.CheckCaseNumberAsync(db, caseNumber, id, ct);
        }

        if (body.AssigneeId is { } assigneeId && assigneeId != request.AssigneeId)
        {
            await errors.CheckAssigneeAsync(db, assigneeId, ct);
        }

        if (errors.Any)
        {
            return errors.ToProblem();
        }

        db.ExpectVersion(request, body.Version, clock.Now);
        var changed = Apply(request, body, caseNumber);
        if (changed.Count > 0)
        {
            audit.Record("request.updated", nameof(InfoRequest), id, AuditDetails.ChangedFields(changed));
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation(RequestRules.CaseNumberIndex))
        {
            return ValidationErrors.Single("caseNumber", RequestRules.DuplicateCaseNumber(caseNumber));
        }

        return TypedResults.Ok((await RequestDetails.LoadAsync(db, id, clock.Today, ct))!);
    }

    public static async Task<Results<Ok<RequestDetail>, NotFound, ValidationProblem>> ChangeStatus(
        Guid id, ChangeStatusRequest body, AppDbContext db, AuditLog audit, AppClock clock, CancellationToken ct)
    {
        var request = await db.Requests.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (request is null)
        {
            return TypedResults.NotFound();
        }

        var today = clock.Today;
        DateOnly? respondedOn = body.Status == RequestStatus.ResponseSent
            ? body.RespondedOn ?? request.RespondedOn ?? today
            : null;
        var errors = new ValidationErrors();
        if (body.Status == RequestStatus.Extended && request.ExtendedDueOn is null)
        {
            errors.Add("status", "Aby oznaczyć sprawę jako przedłużoną, użyj opcji przedłużenia terminu i podaj powód opóźnienia.");
        }

        if (respondedOn > today)
        {
            errors.Add("respondedOn", "Data wysłania odpowiedzi nie może być późniejsza niż dzisiejsza.");
        }
        else if (respondedOn < request.ReceivedOn)
        {
            errors.Add("respondedOn", "Data wysłania odpowiedzi nie może być wcześniejsza niż data wpływu wniosku.");
        }

        if (errors.Any)
        {
            return errors.ToProblem();
        }

        db.ExpectVersion(request, body.Version, clock.Now);
        var details = DescribeStatusChange(request, body.Status, respondedOn);
        request.Status = body.Status;
        request.RespondedOn = respondedOn;
        if (details is not null)
        {
            audit.Record("request.status-changed", nameof(InfoRequest), id, details);
        }

        await db.SaveChangesAsync(ct);
        return TypedResults.Ok((await RequestDetails.LoadAsync(db, id, today, ct))!);
    }

    public static async Task<Results<Ok<ExtensionResult>, NotFound, ValidationProblem>> Extend(
        Guid id,
        ExtendDeadlineRequest body,
        AppDbContext db,
        AuditLog audit,
        AppClock clock,
        IOptions<OrganizationOptions> organization,
        CancellationToken ct)
    {
        var request = await db.Requests.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (request is null)
        {
            return TypedResults.NotFound();
        }

        if (request.IsClosed)
        {
            return ValidationErrors.Single("days", "Nie można przedłużyć terminu w sprawie zakończonej – odpowiedź została już wysłana.");
        }

        var extendedDueOn = Deadlines.ExtendedDueDate(request.DueOn, body.Days);
        var latest = Deadlines.LatestExtendedDueDate(request.ReceivedOn);
        if (extendedDueOn > latest)
        {
            return ValidationErrors.Single("days",
                $"Termin można przedłużyć najpóźniej do {RequestLetters.ShortDate(latest)} r. – nie dłużej niż 2 miesiące " +
                "od dnia złożenia wniosku (art. 13 ust. 2 ustawy o dostępie do informacji publicznej).");
        }

        db.ExpectVersion(request, body.Version, clock.Now);
        request.ExtendedDueOn = extendedDueOn;
        request.ExtensionDays = body.Days;
        request.ExtensionReason = body.Reason.Trim();
        request.Status = RequestStatus.Extended;
        audit.Record("request.extended", nameof(InfoRequest), id,
            $"Nowy termin: {RequestLetters.ShortDate(extendedDueOn)} (+{body.Days} dni)");
        await db.SaveChangesAsync(ct);

        var notice = RequestLetters.ExtensionNotice(request, extendedDueOn, organization.Value, clock.Today);
        return TypedResults.Ok(new ExtensionResult(extendedDueOn, notice));
    }

    private static async Task<(int Sequence, string CaseNumber)> AllocateNumberAsync(
        AppDbContext db, CaseNumberGenerator numbers, int year, CancellationToken ct)
    {
        var (sequence, caseNumber) = await numbers.NextAsync(year, ct);
        while (await db.Requests.AnyAsync(r => r.CaseNumber == caseNumber, ct))
        {
            sequence++;
            caseNumber = numbers.Format(sequence, year);
        }

        return (sequence, caseNumber);
    }

    private static List<string> Apply(InfoRequest request, UpdateRequestRequest body, string caseNumber)
    {
        var changed = new List<string>();

        void Set<T>(T current, T value, string label, Action<T> assign)
        {
            if (!EqualityComparer<T>.Default.Equals(current, value))
            {
                assign(value);
                changed.Add(label);
            }
        }

        var subject = body.Subject.Trim();
        var content = body.Content?.Trim() ?? "";
        if (subject != request.Subject || content != request.Content)
        {
            request.Embedding = null;
        }

        Set(request.CaseNumber, caseNumber, "znak sprawy", v => request.CaseNumber = v);
        Set(request.Subject, subject, "przedmiot", v => request.Subject = v);
        Set(request.Content, content, "treść", v => request.Content = v);
        Set(request.RequesterName, RequestRules.Clean(body.RequesterName), "wnioskodawca", v => request.RequesterName = v);
        Set(request.RequesterContact, RequestRules.Clean(body.RequesterContact), "dane kontaktowe", v => request.RequesterContact = v);
        Set(request.ReplyChannel, body.ReplyChannel, "sposób odpowiedzi", v => request.ReplyChannel = v);
        Set(request.ReceivedOn, body.ReceivedOn, "data wpływu", v => request.ReceivedOn = v);
        Set(request.DueOn, body.DueOn, "termin odpowiedzi", v => request.DueOn = v);
        Set(request.AssigneeId, body.AssigneeId, "osoba prowadząca", v => request.AssigneeId = v);
        return changed;
    }

    private static string? DescribeStatusChange(InfoRequest request, RequestStatus status, DateOnly? respondedOn)
    {
        var sentOn = respondedOn is { } date ? $", data wysłania odpowiedzi: {RequestLetters.ShortDate(date)}" : "";
        if (request.Status != status)
        {
            return AuditDetails.StatusChange(request.Status, status) + sentOn;
        }

        return respondedOn is { } newDate && request.RespondedOn != newDate
            ? $"Data wysłania odpowiedzi: {RequestLetters.ShortDate(newDate)}"
            : null;
    }
}
