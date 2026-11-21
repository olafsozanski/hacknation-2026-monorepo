using ClerkAssistant.Api.Domain;
using Riok.Mapperly.Abstractions;

namespace ClerkAssistant.Api.Features.Knowledge;

public sealed record KnowledgeSummary(
    Guid Id,
    string Title,
    KnowledgeKind Kind,
    string? FileName,
    int ChunkCount,
    int EmbeddedChunkCount,
    DateTimeOffset CreatedAt,
    string CreatedBy);

[Mapper]
public static partial class KnowledgeMapper
{
    [MapPropertyFromSource(nameof(KnowledgeDocumentDto.Indexed), Use = nameof(IsIndexed))]
    public static partial KnowledgeDocumentDto ToDto(this KnowledgeSummary summary);

    private static bool IsIndexed(KnowledgeSummary summary) =>
        summary.ChunkCount > 0 && summary.EmbeddedChunkCount == summary.ChunkCount;
}
