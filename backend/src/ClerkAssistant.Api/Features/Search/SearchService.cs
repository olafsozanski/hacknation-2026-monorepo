using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Infrastructure.Ai;
using ClerkAssistant.Api.Infrastructure.Common;
using ClerkAssistant.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClerkAssistant.Api.Features.Search;

public sealed class SearchService(AppDbContext db, HybridSearch search, AppClock clock)
{
    public const int MaxQueryLength = 1000;
    public const int MaxLimit = 50;
    public const int DefaultLimit = 20;
    public const int SimilarCasesLimit = 8;
    public const string RequestNotFound = "Nie znaleziono wniosku.";

    public static SearchRequest SimilarCasesQuery(string text, DateOnly today, int limit) => new(text)
    {
        PreferReceivedSince = today.AddMonths(-12),
        KnowledgeKinds = [KnowledgeKind.ArchivedResponse],
        Limit = limit,
    };

    public async Task<SearchHit[]> SearchAsync(string? q, int? limit, string? kind, CancellationToken ct)
    {
        var query = q?.Trim() ?? "";
        var take = limit ?? DefaultLimit;
        var errors = new ValidationErrors();
        if (query.Length == 0)
        {
            errors.Add("q", "Wpisz szukaną frazę.");
        }
        else if (query.Length > MaxQueryLength)
        {
            errors.Add("q", $"Szukana fraza może mieć maksymalnie {MaxQueryLength} znaków.");
        }

        if (take is < 1 or > MaxLimit)
        {
            errors.Add("limit", $"Liczba wyników musi mieścić się w zakresie od 1 do {MaxLimit}.");
        }

        var onlyKind = ParseKind(kind, errors);
        errors.ThrowIfAny();

        var outcome = await search.SearchAsync(new SearchRequest(query)
        {
            IncludeRequests = onlyKind is null or SearchHitKind.Request,
            IncludeKnowledge = onlyKind is null or SearchHitKind.Knowledge,
            Limit = take,
        }, ct);
        return outcome.Hits;
    }

    public async Task<SearchHit[]> SimilarAsync(Guid requestId, CancellationToken ct)
    {
        var request = await db.Requests
            .Where(r => r.Id == requestId)
            .Select(r => new { r.Subject, r.Content, Embedding = r.Embedding == null ? null : r.Embedding.Vector })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(RequestNotFound);

        var outcome = await search.SearchAsync(
            SimilarCasesQuery(EmbeddingIndexer.RequestText(request.Subject, request.Content), clock.Today, SimilarCasesLimit) with
            {
                ExcludeRequestId = requestId,
                Embedding = request.Embedding is null ? null : new QueryEmbedding(request.Embedding, false),
            },
            ct);
        return outcome.Hits;
    }

    private static SearchHitKind? ParseKind(string? kind, ValidationErrors errors)
    {
        if (string.IsNullOrWhiteSpace(kind))
        {
            return null;
        }

        if (!char.IsDigit(kind.Trim()[0])
            && Enum.TryParse<SearchHitKind>(kind, ignoreCase: true, out var parsed)
            && Enum.IsDefined(parsed))
        {
            return parsed;
        }

        errors.Add("kind", "Nieprawidłowy rodzaj wyników. Dozwolone wartości: Request, Knowledge.");
        return null;
    }
}
