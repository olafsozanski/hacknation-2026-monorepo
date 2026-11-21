using ClerkAssistant.Api.Features.Search;
using ClerkAssistant.Api.Infrastructure.Ai;
using ClerkAssistant.Api.Infrastructure.Persistence;
using ClerkAssistant.Api.Infrastructure.Security;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ClerkAssistant.Api.Features.Routing;

public static class RoutingEndpoints
{
    public static IServiceCollection AddRoutingFeature(this IServiceCollection services)
    {
        services.TryAddSingleton<AiGateway>();
        services.AddScoped<DepartmentRouter>();
        return services;
    }

    public static void MapRoutingEndpoints(this RouteGroupBuilder api) =>
        api.MapGet("/requests/{id:guid}/department-suggestions", Suggest)
            .RequireRateLimiting(SecurityExtensions.AiRateLimit);

    private static async Task<Results<Ok<DepartmentSuggestion[]>, NotFound>> Suggest(
        Guid id, AppDbContext db, DepartmentRouter router, CancellationToken ct)
    {
        var request = await db.Requests
            .Where(r => r.Id == id)
            .Select(r => new { r.Subject, r.Content, Embedding = r.Embedding == null ? null : r.Embedding.Vector })
            .FirstOrDefaultAsync(ct);
        if (request is null)
        {
            return TypedResults.NotFound();
        }

        var outcome = await router.SuggestAsync(
            EmbeddingIndexer.RequestText(request.Subject, request.Content),
            id,
            request.Embedding is null ? null : new QueryEmbedding(request.Embedding, false),
            ct);
        return TypedResults.Ok(outcome.Suggestions);
    }
}
