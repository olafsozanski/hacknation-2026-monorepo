using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Features.Drafts;
using ClerkAssistant.Api.Infrastructure.Common;
using ClerkAssistant.Api.Infrastructure.Documents;
using ClerkAssistant.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClerkAssistant.Api.Features.Knowledge;

public sealed class KnowledgeService(
    AppDbContext db, DocumentReader reader, CurrentUser user, AuditLog audit, AppClock clock)
{
    public const int MaxTitleLength = 500;
    public const string DocumentNotFound = "Nie znaleziono dokumentu w bazie wiedzy.";

    public async Task<KnowledgeDocumentDto[]> ListAsync(CancellationToken ct)
    {
        var summaries = await Summaries(db.KnowledgeDocuments.OrderByDescending(d => d.CreatedAt)).ToListAsync(ct);
        return summaries.Select(s => s.ToDto()).ToArray();
    }

    public async Task<KnowledgeDocumentDto> UploadAsync(IFormFile? file, string? title, string? kind, CancellationToken ct)
    {
        var errors = new ValidationErrors();
        if (file is null)
        {
            errors.Add("file", "Wybierz plik do dodania.");
        }

        if (!TryParseKind(kind, out var knowledgeKind))
        {
            errors.Add("kind", "Nieprawidłowy rodzaj dokumentu.");
        }

        var cleanTitle = title?.Trim();
        if (cleanTitle?.Length > MaxTitleLength)
        {
            errors.Add("title", $"Tytuł może mieć maksymalnie {MaxTitleLength} znaków.");
        }

        errors.ThrowIfAny();
        var document = await reader.ReadAsync(file!, ct);
        if (!document.HasText)
        {
            throw ValidationException.For("file",
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

        var created = await Summaries(db.KnowledgeDocuments.Where(d => d.Id == knowledge.Id)).FirstAsync(ct);
        return created.ToDto();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var document = await db.KnowledgeDocuments
            .Where(d => d.Id == id)
            .Select(d => new { d.FileId, d.Kind })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(DocumentNotFound);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.KnowledgeDocuments.Where(d => d.Id == id).ExecuteDeleteAsync(ct);
        if (document.FileId is { } fileId)
        {
            await db.Files.Where(f => f.Id == fileId).ExecuteDeleteAsync(ct);
        }

        audit.Record("knowledge.deleted", nameof(KnowledgeDocument), id, document.Kind.ToString());
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task<FileDownload> GetContentAsync(Guid id, CancellationToken ct) =>
        await db.KnowledgeDocuments
            .Where(d => d.Id == id && d.File != null)
            .Select(d => new FileDownload(d.File!.Content, d.File.ContentType, d.File.FileName))
            .FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException(DocumentNotFound);

    private IQueryable<KnowledgeSummary> Summaries(IQueryable<KnowledgeDocument> documents) => documents.Select(d => new KnowledgeSummary(
        d.Id,
        d.Title,
        d.Kind,
        d.File != null ? d.File.FileName : null,
        d.Chunks.Count,
        d.Chunks.Count(c => c.Embedding != null),
        d.CreatedAt,
        db.Users.Where(u => u.Id == d.CreatedById).Select(u => u.FullName).FirstOrDefault() ?? ""));

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
}
