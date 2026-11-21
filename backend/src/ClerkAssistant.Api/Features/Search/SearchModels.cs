using ClerkAssistant.Api.Domain;
using Pgvector;

namespace ClerkAssistant.Api.Features.Search;

public sealed record QueryEmbedding(Vector? Vector, bool Failed)
{
    public static QueryEmbedding None { get; } = new(null, false);
}

public sealed record SearchRequest(string Text)
{
    public bool IncludeRequests { get; init; } = true;
    public bool IncludeKnowledge { get; init; } = true;
    public DateOnly? RequestsReceivedSince { get; init; }
    public DateOnly? PreferReceivedSince { get; init; }
    public IReadOnlyCollection<KnowledgeKind>? KnowledgeKinds { get; init; }
    public Guid? ExcludeRequestId { get; init; }
    public int Limit { get; init; } = 20;
    public QueryEmbedding? Embedding { get; init; }
}

public sealed record SearchOutcome(SearchHit[] Hits, bool AiFailed);

public sealed record KnowledgePassage(Guid DocumentId, string Title, KnowledgeKind Kind, string Text);

internal sealed record RequestHitSource(
    Guid Id,
    string Subject,
    string CaseNumber,
    DateOnly ReceivedOn,
    RequestStatus Status,
    string Content,
    string? DraftContent,
    string[] DepartmentSymbols);

internal sealed record KnowledgeHitSource(
    Guid DocumentId, string Title, KnowledgeKind KnowledgeKind, DateTimeOffset CreatedAt, string Text);
