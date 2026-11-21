using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Infrastructure.Common;
using ClerkAssistant.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ClerkAssistant.Api.Features.Requests;

public sealed record KnownDepartment(Guid Id, string Symbol);

public static class RequestRules
{
    public const string CaseNumberIndex = "ix_requests_case_number";

    public static NotFoundException RequestNotFound() => new("Nie znaleziono wniosku.");

    public static async Task<InfoRequest> FindRequestAsync(this AppDbContext db, Guid id, CancellationToken ct) =>
        await db.Requests.FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw RequestNotFound();

    public static ValidationException DuplicateCaseNumberError(string caseNumber) =>
        ValidationException.For("caseNumber", DuplicateCaseNumber(caseNumber));

    public const string UnknownStatus = "Nieprawidłowy status sprawy.";
    public const string UnknownReplyChannel = "Nieprawidłowy sposób udzielenia odpowiedzi.";

    public static void CheckDefined<TEnum>(this ValidationErrors errors, string field, TEnum value, string message)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            errors.Add(field, message);
        }
    }

    public static void CheckDates(this ValidationErrors errors, DateOnly receivedOn, DateOnly dueOn, DateOnly today)
    {
        if (receivedOn > today)
        {
            errors.Add("receivedOn", "Data wpływu nie może być późniejsza niż dzisiejsza.");
        }

        if (dueOn < receivedOn)
        {
            errors.Add("dueOn", "Termin odpowiedzi nie może być wcześniejszy niż data wpływu.");
        }
    }

    public static async Task CheckCaseNumberAsync(
        this ValidationErrors errors, AppDbContext db, string caseNumber, Guid? exceptRequestId, CancellationToken ct)
    {
        if (await db.Requests.AnyAsync(r => r.CaseNumber == caseNumber && r.Id != exceptRequestId, ct))
        {
            errors.Add("caseNumber", DuplicateCaseNumber(caseNumber));
        }
    }

    public static async Task CheckAssigneeAsync(this ValidationErrors errors, AppDbContext db, Guid assigneeId, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(u => u.Id == assigneeId && u.IsActive, ct))
        {
            errors.Add("assigneeId", "Wybrana osoba prowadząca nie istnieje lub jej konto jest nieaktywne.");
        }
    }

    public static async Task<List<KnownDepartment>> CheckDepartmentsAsync(
        this ValidationErrors errors, AppDbContext db, IEnumerable<Guid> departmentIds, CancellationToken ct)
    {
        var ids = departmentIds.Distinct().ToArray();
        var known = await db.Departments
            .Where(d => ids.Contains(d.Id))
            .OrderBy(d => d.Symbol)
            .Select(d => new KnownDepartment(d.Id, d.Symbol))
            .ToListAsync(ct);
        if (known.Count != ids.Length)
        {
            errors.Add("departmentIds", "Wybrano wydział, który nie istnieje w słowniku.");
        }

        return known;
    }

    public static async Task<string?> CheckSourceFileAsync(
        this ValidationErrors errors, AppDbContext db, Guid fileId, CancellationToken ct)
    {
        var file = await db.Files
            .Where(f => f.Id == fileId)
            .Select(f => new
            {
                f.ExtractedText,
                Attached = db.RequestDocuments.Any(d => d.FileId == f.Id) || db.KnowledgeDocuments.Any(k => k.FileId == f.Id),
            })
            .FirstOrDefaultAsync(ct);
        if (file is null)
        {
            errors.Add("sourceFileId", "Nie znaleziono pliku wniosku. Prześlij go ponownie.");
            return null;
        }

        if (file.Attached)
        {
            errors.Add("sourceFileId", "Ten plik jest już dołączony do innej sprawy lub do bazy wiedzy.");
            return null;
        }

        return file.ExtractedText;
    }

    public static string DuplicateCaseNumber(string caseNumber) =>
        $"Sprawa o znaku {caseNumber} jest już zarejestrowana.";

    public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static bool IsUniqueViolation(this DbUpdateException exception, string? constraint = null) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres
        && (constraint is null || postgres.ConstraintName == constraint);

    public static void ExpectVersion(this AppDbContext db, InfoRequest request, uint version, DateTimeOffset now)
    {
        var entry = db.Entry(request);
        entry.Property(r => r.Version).OriginalValue = version;
        request.UpdatedAt = now;
        entry.Property(r => r.UpdatedAt).IsModified = true;
    }
}
