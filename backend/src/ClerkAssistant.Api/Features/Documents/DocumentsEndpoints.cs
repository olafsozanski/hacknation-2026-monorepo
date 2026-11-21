using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Features.Requests;
using ClerkAssistant.Api.Infrastructure.Common;
using ClerkAssistant.Api.Infrastructure.Documents;
using ClerkAssistant.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClerkAssistant.Api.Features.Documents;

public static class DocumentsEndpoints
{
    public static IServiceCollection AddDocumentsFeature(this IServiceCollection services) => services;

    public static void MapDocumentsEndpoints(this RouteGroupBuilder api)
    {
        // CSRF is validated by AntiforgeryFilter on the whole /api group (header token, not a form field).
        api.MapPost("/requests/{id:guid}/documents", Upload).DisableAntiforgery();

        var documents = api.MapGroup("/documents");
        documents.MapGet("/{id:guid}/content", Download);
        documents.MapGet("/{id:guid}/text", GetText);
        documents.MapDelete("/{id:guid}", Delete);
    }

    private static async Task<Results<Created<DocumentDto>, NotFound, ValidationProblem>> Upload(
        Guid id,
        IFormFile? file,
        [FromForm] DocumentKind? kind,
        [FromForm] Guid? departmentId,
        AppDbContext db,
        DocumentReader reader,
        CurrentUser current,
        AuditLog audit,
        AppClock clock,
        CancellationToken ct)
    {
        if (!await db.Requests.AnyAsync(r => r.Id == id, ct))
        {
            return TypedResults.NotFound();
        }

        if (file is null)
        {
            return ValidationErrors.Single("file", "Wybierz plik do przesłania.");
        }

        var department = departmentId is { } deptId ? await db.Departments.FirstOrDefaultAsync(d => d.Id == deptId, ct) : null;
        if (departmentId is not null && department is null)
        {
            return ValidationErrors.Single("departmentId", "Wybrany wydział nie istnieje w słowniku.");
        }

        ReadDocument read;
        try
        {
            read = await reader.ReadAsync(file, ct);
        }
        catch (DocumentRejectedException ex)
        {
            return ValidationErrors.Single("file", ex.Message);
        }

        var documentKind = kind ?? DocumentKind.DepartmentReply;
        var document = new RequestDocument
        {
            Id = Guid.CreateVersion7(),
            RequestId = id,
            Kind = documentKind,
            DepartmentId = department?.Id,
            File = read.ToStoredFile(current.Id, clock.Now),
            UploadedAt = clock.Now,
            UploadedById = current.Id,
        };
        db.RequestDocuments.Add(document);
        if (documentKind == DocumentKind.DepartmentReply && department is not null)
        {
            await MarkAnsweredAsync(db, id, department.Id, clock.Today, ct);
        }

        audit.Record("document.uploaded", nameof(InfoRequest), id, Describe(documentKind, department?.Symbol, read.FileName));
        await db.SaveChangesAsync(ct);

        var uploadedBy = await db.Users.Where(u => u.Id == current.Id).Select(u => u.FullName).FirstOrDefaultAsync(ct) ?? "";
        return TypedResults.Created($"/api/documents/{document.Id}/content", new DocumentDto(
            document.Id,
            document.Kind,
            read.FileName,
            read.Content.LongLength,
            department?.Id,
            department?.Symbol,
            document.UploadedAt,
            uploadedBy,
            read.HasText));
    }

    private static async Task<Results<FileContentHttpResult, NotFound>> Download(Guid id, AppDbContext db, CancellationToken ct)
    {
        var file = await db.RequestDocuments
            .Where(d => d.Id == id)
            .Select(d => new { d.File.Content, d.File.ContentType, d.File.FileName })
            .FirstOrDefaultAsync(ct);
        return file is null
            ? TypedResults.NotFound()
            : TypedResults.File(file.Content, file.ContentType, file.FileName);
    }

    private static async Task<Results<Ok<DocumentText>, NotFound>> GetText(Guid id, AppDbContext db, CancellationToken ct)
    {
        var text = await db.RequestDocuments
            .Where(d => d.Id == id)
            .Select(d => d.File.ExtractedText)
            .FirstOrDefaultAsync(ct);
        return text is null ? TypedResults.NotFound() : TypedResults.Ok(new DocumentText(text));
    }

    private static async Task<Results<NoContent, NotFound>> Delete(Guid id, AppDbContext db, AuditLog audit, CancellationToken ct)
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
            .FirstOrDefaultAsync(ct);
        if (document is null)
        {
            return TypedResults.NotFound();
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Files.Where(f => f.Id == document.FileId).ExecuteDeleteAsync(ct);
        audit.Record("document.deleted", nameof(InfoRequest), document.RequestId,
            Describe(document.Kind, document.DepartmentSymbol, document.FileName));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task MarkAnsweredAsync(AppDbContext db, Guid requestId, Guid departmentId, DateOnly today, CancellationToken ct)
    {
        var link = await db.RequestDepartments.FirstOrDefaultAsync(l => l.RequestId == requestId && l.DepartmentId == departmentId, ct);
        if (link is null)
        {
            db.RequestDepartments.Add(new RequestDepartment { RequestId = requestId, DepartmentId = departmentId, AnsweredOn = today });
        }
        else
        {
            link.AnsweredOn ??= today;
        }
    }

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
