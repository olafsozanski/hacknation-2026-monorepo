using ClerkAssistant.Api.Domain;

namespace ClerkAssistant.Api.Features.Knowledge;

public record KnowledgeDocumentDto(
    Guid Id,
    string Title,
    KnowledgeKind Kind,
    string? FileName,
    int ChunkCount,
    bool Indexed,
    DateTimeOffset CreatedAt,
    string CreatedBy);
