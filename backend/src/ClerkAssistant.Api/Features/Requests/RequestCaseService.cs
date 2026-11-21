using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Features.Documents;
using ClerkAssistant.Api.Infrastructure.Common;
using ClerkAssistant.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClerkAssistant.Api.Features.Requests;

public sealed class RequestCaseService(
    AppDbContext db,
    CaseNumberGenerator numbers,
    DocumentService documents,
    CurrentUser current,
    AuditLog audit,
    AppClock clock)
{
    private const string SubjectLabel = "przedmiot";
    private const string ContentLabel = "treść";

    public async Task<RequestDetail> GetAsync(Guid id, CancellationToken ct)
    {
        var request = await db.Requests
            .AsNoTracking()
            .Include(r => r.Assignee)
            .Include(r => r.Departments).ThenInclude(d => d.Department)
            .Include(r => r.Draft)
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw RequestRules.RequestNotFound();

        var today = clock.Today;
        return request.ToDetail(
            Deadlines.LatestExtendedDueDate(request.ReceivedOn),
            Deadlines.UrgencyOf(request, today),
            Deadlines.DaysLeft(request.EffectiveDueOn, today),
            await documents.ListForRequestAsync(id, ct));
    }

    public async Task<CreatedResponse> CreateAsync(CreateRequestRequest body, CancellationToken ct)
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
        errors.ThrowIfAny();

        var content = string.IsNullOrWhiteSpace(body.Content) ? sourceText?.Trim() ?? "" : body.Content.Trim();
        for (var attempt = 1; ; attempt++)
        {
            var (sequence, generatedNumber) = await AllocateNumberAsync(body.ReceivedOn.Year, ct);
            var request = NewRequest(body, sequence, caseNumber ?? generatedNumber, dueOn, content, departments);
            db.Requests.Add(request);
            audit.Record("request.created", nameof(InfoRequest), request.Id, request.CaseNumber);
            try
            {
                await db.SaveChangesAsync(ct);
                return request.ToCreatedResponse();
            }
            catch (DbUpdateException ex) when (ex.IsUniqueViolation())
            {
                db.ChangeTracker.Clear();
                if (caseNumber is not null && ex.IsUniqueViolation(RequestRules.CaseNumberIndex))
                {
                    throw RequestRules.DuplicateCaseNumberError(caseNumber);
                }

                if (attempt > 1)
                {
                    throw;
                }
            }
        }
    }

    public async Task<RequestDetail> UpdateAsync(Guid id, UpdateRequestRequest body, CancellationToken ct)
    {
        var request = await db.FindRequestAsync(id, ct);
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

        errors.ThrowIfAny();

        db.ExpectVersion(request, body.Version, clock.Now);
        var changed = Apply(request, body, caseNumber);
        if (changed.Count > 0)
        {
            audit.Record("request.updated", nameof(InfoRequest), id, AuditDetails.ChangedFields(changed));
        }

        await using (var transaction = await db.Database.BeginTransactionAsync(ct))
        {
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (ex.IsUniqueViolation(RequestRules.CaseNumberIndex))
            {
                throw RequestRules.DuplicateCaseNumberError(caseNumber);
            }

            if (changed.Contains(SubjectLabel) || changed.Contains(ContentLabel))
            {
                await db.RequestEmbeddings.Where(e => e.RequestId == id).ExecuteDeleteAsync(ct);
            }

            await transaction.CommitAsync(ct);
        }

        return await GetAsync(id, ct);
    }

    public async Task<HistoryItem[]> HistoryAsync(Guid id, CancellationToken ct)
    {
        var entries = await db.AuditEntries
            .AsNoTracking()
            .Where(e => e.EntityType == nameof(InfoRequest) && e.EntityId == id)
            .OrderByDescending(e => e.At)
            .ThenByDescending(e => e.Id)
            .ToListAsync(ct);
        var userIds = entries.Where(e => e.UserId is not null).Select(e => e.UserId!.Value).Distinct().ToList();
        var names = await db.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        return entries
            .Select(e => e.ToHistoryItem(e.UserId is { } userId && names.TryGetValue(userId, out var name) ? name : e.UserName))
            .ToArray();
    }

    private InfoRequest NewRequest(
        CreateRequestRequest body, int sequence, string caseNumber, DateOnly dueOn, string content, List<KnownDepartment> departments)
    {
        var request = new InfoRequest
        {
            Id = Guid.CreateVersion7(),
            Year = body.ReceivedOn.Year,
            SequenceNumber = sequence,
            CaseNumber = caseNumber,
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

        return request;
    }

    private async Task<(int Sequence, string CaseNumber)> AllocateNumberAsync(int year, CancellationToken ct)
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

        Set(request.CaseNumber, caseNumber, "znak sprawy", v => request.CaseNumber = v);
        Set(request.Subject, body.Subject.Trim(), SubjectLabel, v => request.Subject = v);
        Set(request.Content, body.Content?.Trim() ?? "", ContentLabel, v => request.Content = v);
        Set(request.RequesterName, RequestRules.Clean(body.RequesterName), "wnioskodawca", v => request.RequesterName = v);
        Set(request.RequesterContact, RequestRules.Clean(body.RequesterContact), "dane kontaktowe", v => request.RequesterContact = v);
        Set(request.ReplyChannel, body.ReplyChannel, "sposób odpowiedzi", v => request.ReplyChannel = v);
        Set(request.ReceivedOn, body.ReceivedOn, "data wpływu", v => request.ReceivedOn = v);
        Set(request.DueOn, body.DueOn, "termin odpowiedzi", v => request.DueOn = v);
        Set(request.AssigneeId, body.AssigneeId, "osoba prowadząca", v => request.AssigneeId = v);
        return changed;
    }
}
