using ClerkAssistant.Api.Infrastructure.Ai;
using ClerkAssistant.Api.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ClerkAssistant.Api.Features.Search;

public static class SearchEndpoints
{
    public static IServiceCollection AddSearchFeature(this IServiceCollection services)
    {
        services.TryAddSingleton<AiGateway>();
        services.AddSingleton<TermStatistics>();
        services.AddScoped<HybridSearch>();
        services.AddScoped<SearchService>();
        services.AddSingleton<EmbeddingIndexer>();
        services.AddHostedService(sp => sp.GetRequiredService<EmbeddingIndexer>());
        return services;
    }

    public static void MapSearchEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/search", (string? q, int? limit, string? kind, SearchService search, CancellationToken ct) =>
                search.SearchAsync(q, limit, kind, ct))
            .RequireRateLimiting(SecurityExtensions.SearchRateLimit);

        api.MapGet("/requests/{id:guid}/similar", (Guid id, SearchService search, CancellationToken ct) =>
            search.SimilarAsync(id, ct));
    }
}
