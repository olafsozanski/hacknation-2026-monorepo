using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Infrastructure.Common;
using ClerkAssistant.Api.Infrastructure.Documents;
using ClerkAssistant.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClerkAssistant.Api.Features.Knowledge;

public static class KnowledgeEndpoints
{
    private const int MaxTitleLength = 500;

    public static IServiceCollection AddKnowledgeFeature(this IServiceCollection services) => services;

    public static void MapKnowledgeEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/knowledge");
        group.MapGet("/", List);
        group.MapPost("/", Upload);
        group.MapDelete("/{id:guid}", Delete);
        group.MapGet("/{id:guid}/content", Download);
    }

    private static async Task<KnowledgeDocumentDto[]> List(AppDbContext db, CancellationToken ct) =>
        await db.KnowledgeDocuments
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new KnowledgeDocumentDto(
                d.Id,
                d.Title,
                d.Kind,
                d.File != null ? d.File.FileName : null,
                d.Chunks.Count,
                d.Chunks.Count > 0 && d.Chunks.All(c => c.Embedding != null),
                d.CreatedAt,
                db.Users.Where(u => u.Id == d.CreatedById).Select(u => u.FullName).FirstOrDefault() ?? ""))
            .ToArrayAsync(ct);

    private static async Task<Results<Created<KnowledgeDocumentDto>, ValidationProblem>> Upload(
        IFormFile? file,
        [FromForm] string? title,
        [FromForm] string? kind,
        DocumentReader reader,
        AppDbContext db,
        CurrentUser user,
        AuditLog audit,
        AppClock clock,
        CancellationToken ct)
    {
        if (file is null)
        {
            return Invalid("file", "Wybierz plik do dodania.");
        }

        if (!TryParseKind(kind, out var knowledgeKind))
        {
            return Invalid("kind", "Nieprawidłowy rodzaj dokumentu.");
        }

        var cleanTitle = title?.Trim();
        if (cleanTitle?.Length > MaxTitleLength)
        {
            return Invalid("title", $"Tytuł może mieć maksymalnie {MaxTitleLength} znaków.");
        }

        ReadDocument document;
        try
        {
            document = await reader.ReadAsync(file, ct);
        }
        catch (DocumentRejectedException ex)
        {
            return Invalid("file", ex.Message);
        }

        if (!document.HasText)
        {
            return Invalid("file",
                "Nie odczytano tekstu z dokumentu — może to być skan. Do bazy wiedzy można dodać tylko dokumenty z warstwą tekstową.");
        }

        var knowledge = new KnowledgeDocument
        {
            Title = string.IsNullOrEmpty(cleanTitle) ? Path.GetFileNameWithoutExtension(document.FileName) : cleanTitle,
            Kind = knowledgeKind,
            File = document.ToStoredFile(user.Id, clock.Now),
            Text = document.Text,
            CreatedAt = clock.Now,
            CreatedById = user.Id,
            Chunks = TextChunker.Split(document.Text)
                .Select((text, index) => new KnowledgeChunk { Index = index, Text = text })
                .ToList(),
        };
        db.KnowledgeDocuments.Add(knowledge);
        audit.Record("knowledge.uploaded", nameof(KnowledgeDocument), knowledge.Id, knowledgeKind.ToString());
        await db.SaveChangesAsync(ct);

        var createdBy = await db.Users.Where(u => u.Id == user.Id).Select(u => u.FullName).FirstOrDefaultAsync(ct);
        return TypedResults.Created($"/api/knowledge/{knowledge.Id}", new KnowledgeDocumentDto(
            knowledge.Id,
            knowledge.Title,
            knowledge.Kind,
            knowledge.File.FileName,
            knowledge.Chunks.Count,
            false,
            knowledge.CreatedAt,
            createdBy ?? ""));
    }

    private static async Task<Results<NoContent, NotFound>> Delete(
        Guid id, AppDbContext db, AuditLog audit, CancellationToken ct)
    {
        var document = await db.KnowledgeDocuments
            .Where(d => d.Id == id)
            .Select(d => new { d.FileId, d.Kind })
            .FirstOrDefaultAsync(ct);
        if (document is null)
        {
            return TypedResults.NotFound();
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.KnowledgeDocuments.Where(d => d.Id == id).ExecuteDeleteAsync(ct);
        if (document.FileId is { } fileId)
        {
            await db.Files.Where(f => f.Id == fileId).ExecuteDeleteAsync(ct);
        }

        audit.Record("knowledge.deleted", nameof(KnowledgeDocument), id, document.Kind.ToString());
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<Results<FileContentHttpResult, NotFound>> Download(
        Guid id, AppDbContext db, CancellationToken ct)
    {
        var file = await db.KnowledgeDocuments
            .Where(d => d.Id == id && d.File != null)
            .Select(d => new { d.File!.Content, d.File.ContentType, d.File.FileName })
            .FirstOrDefaultAsync(ct);
        return file is null
            ? TypedResults.NotFound()
            : TypedResults.File(file.Content, file.ContentType, file.FileName);
    }

    private static bool TryParseKind(string? value, out KnowledgeKind kind)
    {
        kind = KnowledgeKind.Other;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        return !char.IsDigit(value.Trim()[0])
               && Enum.TryParse(value.Trim(), ignoreCase: true, out kind)
               && Enum.IsDefined(kind);
    }

    private static ValidationProblem Invalid(string field, string message) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });
}
