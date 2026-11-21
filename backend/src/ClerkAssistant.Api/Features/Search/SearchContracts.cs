using ClerkAssistant.Api.Domain;

namespace ClerkAssistant.Api.Features.Search;

public enum SearchHitKind
{
    Request,
    Knowledge,
}

public record SearchHit(
    SearchHitKind Kind,
    Guid Id,
    string Title,
    string? CaseNumber,
    DateOnly? Date,
    string Snippet,
    double Score,
    RequestStatus? Status,
    string[] DepartmentSymbols,
    KnowledgeKind? KnowledgeKind);
