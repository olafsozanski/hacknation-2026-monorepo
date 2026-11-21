using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Infrastructure.Ai;
using ClerkAssistant.Api.Infrastructure.Common;
using ClerkAssistant.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ClerkAssistant.Api.Features.Search;

public static class SearchEndpoints
{
    public const int MaxQueryLength = 1000;
    public const int MaxLimit = 50;
    public const int SimilarCasesLimit = 8;

    public static IServiceCollection AddSearchFeature(this IServiceCollection services)
    {
        services.TryAddSingleton<AiGateway>();
        services.AddScoped<HybridSearch>();
        services.AddSingleton<EmbeddingIndexer>();
        services.AddHostedService(sp => sp.GetRequiredService<EmbeddingIndexer>());
        return services;
    }

    public static void MapSearchEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/search", Search);
        api.MapGet("/requests/{id:guid}/similar", Similar);
    }

    public static SearchRequest SimilarCases(string text, DateOnly today, int limit) => new(text)
    {
        RequestsReceivedSince = today.AddMonths(-12),
        KnowledgeKinds = [KnowledgeKind.ArchivedResponse],
        Limit = limit,
    };

    private static async Task<Results<Ok<SearchHit[]>, ValidationProblem>> Search(
        string? q, int? limit, string? kind, HybridSearch search, CancellationToken ct)
    {
        var query = q?.Trim() ?? "";
        if (query.Length == 0)
        {
            return Invalid("q", "Wpisz szukaną frazę.");
        }

        if (query.Length > MaxQueryLength)
        {
            return Invalid("q", $"Szukana fraza może mieć maksymalnie {MaxQueryLength} znaków.");
        }

        var take = limit ?? 20;
        if (take is < 1 or > MaxLimit)
        {
            return Invalid("limit", $"Liczba wyników musi mieścić się w zakresie od 1 do {MaxLimit}.");
        }

        SearchHitKind? onlyKind = null;
        if (!string.IsNullOrWhiteSpace(kind))
        {
            if (!Enum.TryParse<SearchHitKind>(kind, ignoreCase: true, out var parsed)
                || !Enum.IsDefined(parsed)
                || char.IsDigit(kind.Trim()[0]))
            {
                return Invalid("kind", "Nieprawidłowy rodzaj wyników. Dozwolone wartości: Request, Knowledge.");
            }

            onlyKind = parsed;
        }

        var outcome = await search.SearchAsync(new SearchRequest(query)
        {
            IncludeRequests = onlyKind is null or SearchHitKind.Request,
            IncludeKnowledge = onlyKind is null or SearchHitKind.Knowledge,
            Limit = take,
        }, ct);
        return TypedResults.Ok(outcome.Hits);
    }

    private static async Task<Results<Ok<SearchHit[]>, NotFound>> Similar(
        Guid id, AppDbContext db, HybridSearch search, AppClock clock, CancellationToken ct)
    {
        var request = await db.Requests
            .Where(r => r.Id == id)
            .Select(r => new { r.Subject, r.Content, Embedding = r.Embedding == null ? null : r.Embedding.Vector })
            .FirstOrDefaultAsync(ct);
        if (request is null)
        {
            return TypedResults.NotFound();
        }

        var outcome = await search.SearchAsync(
            SimilarCases(EmbeddingIndexer.RequestText(request.Subject, request.Content), clock.Today, SimilarCasesLimit) with
            {
                ExcludeRequestId = id,
                Embedding = request.Embedding is null ? null : new QueryEmbedding(request.Embedding, false),
            },
            ct);
        return TypedResults.Ok(outcome.Hits);
    }

    private static ValidationProblem Invalid(string field, string message) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });
}
