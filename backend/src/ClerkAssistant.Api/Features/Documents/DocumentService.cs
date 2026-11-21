using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Infrastructure.Common;
using ClerkAssistant.Api.Infrastructure.Documents;
using ClerkAssistant.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClerkAssistant.Api.Features.Documents;

public sealed class DocumentService(
    AppDbContext db,
    DocumentReader reader,
    CurrentUser current,
    AuditLog audit,
    AppClock clock)
{
    public async Task<DocumentDto[]> ListForRequestAsync(Guid requestId, CancellationToken ct) =>
        await ToDtosAsync(db.RequestDocuments.Where(d => d.RequestId == requestId).OrderBy(d => d.UploadedAt), ct);

    public async Task<DocumentDto> UploadAsync(
        Guid requestId, IFormFile? file, DocumentKind? kind, Guid? departmentId, CancellationToken ct)
    {
        if (!await db.Requests.AnyAsync(r => r.Id == requestId, ct))
        {
            throw new NotFoundException("Nie znaleziono wniosku.");
        }

        if (file is null)
        {
            throw ValidationException.For("file", "Wybierz plik do przesłania.");
        }

        var department = departmentId is { } id ? await db.Departments.FirstOrDefaultAsync(d => d.Id == id, ct) : null;
        if (departmentId is not null && department is null)
        {
            throw ValidationException.For("departmentId", "Wybrany wydział nie istnieje w słowniku.");
        }

        var read = await reader.ReadAsync(file, ct);
        var documentKind = kind ?? DocumentKind.DepartmentReply;
        var document = new RequestDocument
        {
            Id = Guid.CreateVersion7(),
            RequestId = requestId,
            Kind = documentKind,
            DepartmentId = department?.Id,
            File = read.ToStoredFile(current.Id, clock.Now),
            UploadedAt = clock.Now,
            UploadedById = current.Id,
        };
        db.RequestDocuments.Add(document);
        if (documentKind == DocumentKind.DepartmentReply && department is not null)
        {
            await MarkAnsweredAsync(requestId, department.Id, ct);
        }

        audit.Record("document.uploaded", nameof(InfoRequest), requestId, Describe(documentKind, department?.Symbol, read.FileName));
        await db.SaveChangesAsync(ct);
        return (await ToDtosAsync(db.RequestDocuments.Where(d => d.Id == document.Id), ct)).Single();
    }

    public async Task<DocumentContent> DownloadAsync(Guid id, CancellationToken ct) =>
        await FileOf(id).ProjectToContent().FirstOrDefaultAsync(ct) ?? throw DocumentNotFound();

    public async Task<DocumentText> GetTextAsync(Guid id, CancellationToken ct) =>
        await FileOf(id).ProjectToText().FirstOrDefaultAsync(ct) ?? throw DocumentNotFound();

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var document = await db.RequestDocuments
            .Where(d => d.Id == id)
            .Select(d => new
            {
                d.RequestId,
                d.FileId,
                d.Kind,
                DepartmentSymbol = d.Department == null ? null : d.Department.Symbol,
                d.File.FileName,
            })
            .FirstOrDefaultAsync(ct)
            ?? throw DocumentNotFound();

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Files.Where(f => f.Id == document.FileId).ExecuteDeleteAsync(ct);
        audit.Record("document.deleted", nameof(InfoRequest), document.RequestId,
            Describe(document.Kind, document.DepartmentSymbol, document.FileName));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private async Task<DocumentDto[]> ToDtosAsync(IQueryable<RequestDocument> documents, CancellationToken ct)
    {
        var metadata = await documents.ProjectToMetadata().ToListAsync(ct);
        var uploaderIds = metadata.Select(d => d.UploadedById).Distinct().ToList();
        var uploaders = await db.Users
            .Where(u => uploaderIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        return metadata.Select(d => d.ToDto(uploaders.GetValueOrDefault(d.UploadedById, ""))).ToArray();
    }

    private IQueryable<StoredFile> FileOf(Guid documentId) =>
        db.RequestDocuments.Where(d => d.Id == documentId).Select(d => d.File);

    private async Task MarkAnsweredAsync(Guid requestId, Guid departmentId, CancellationToken ct)
    {
        var link = await db.RequestDepartments.FirstOrDefaultAsync(l => l.RequestId == requestId && l.DepartmentId == departmentId, ct);
        if (link is null)
        {
            db.RequestDepartments.Add(new RequestDepartment { RequestId = requestId, DepartmentId = departmentId, AnsweredOn = clock.Today });
        }
        else
        {
            link.AnsweredOn ??= clock.Today;
        }
    }

    private static NotFoundException DocumentNotFound() => new("Nie znaleziono dokumentu.");

    private static string Describe(DocumentKind kind, string? departmentSymbol, string fileName)
    {
        var label = kind switch
        {
            DocumentKind.Request => "Wniosek",
            DocumentKind.DepartmentReply => "Odpowiedź wydziału",
            _ => "Załącznik",
        };
        var department = departmentSymbol is null ? "" : $" {departmentSymbol}";
        var format = Path.GetExtension(fileName).TrimStart('.').ToUpperInvariant();
        return format.Length == 0 ? label + department : $"{label}{department} ({format})";
    }
}
