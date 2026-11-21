using NpgsqlTypes;
using Pgvector;

namespace ClerkAssistant.Api.Domain;

public enum KnowledgeKind
{
    Law,
    Ordinance,
    ArchivedResponse,
    Guideline,
    Other,
}

public class KnowledgeDocument
{
    public Guid Id { get; set; }
    public required string Title { get; set; }
    public KnowledgeKind Kind { get; set; }
    public Guid? FileId { get; set; }
    public StoredFile? File { get; set; }
    public string Text { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public Guid CreatedById { get; set; }
    public List<KnowledgeChunk> Chunks { get; set; } = [];
}

public class KnowledgeChunk
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public KnowledgeDocument Document { get; set; } = null!;
    public int Index { get; set; }
    public required string Text { get; set; }
    public Vector? Embedding { get; set; }
    public NpgsqlTsVector SearchVector { get; set; } = null!;
}
