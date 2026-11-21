using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Infrastructure.Security;
using Microsoft.AspNetCore.Mvc;

namespace ClerkAssistant.Api.Features.Knowledge;

public static class KnowledgeEndpoints
{
    public static IServiceCollection AddKnowledgeFeature(this IServiceCollection services) =>
        services.AddScoped<KnowledgeService>();

    public static void MapKnowledgeEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/knowledge");
        group.MapGet("/", (KnowledgeService knowledge, CancellationToken ct) => knowledge.ListAsync(ct));
        group.MapPost("/", async (
                IFormFile? file, [FromForm] string? title, [FromForm] string? kind, KnowledgeService knowledge, CancellationToken ct) =>
            {
                var created = await knowledge.UploadAsync(file, title, kind, ct);
                return TypedResults.Created($"/api/knowledge/{created.Id}", created);
            })
            .RequireRateLimiting(SecurityExtensions.UploadRateLimit);
        group.MapDelete("/{id:guid}", async (Guid id, KnowledgeService knowledge, CancellationToken ct) =>
            {
                await knowledge.DeleteAsync(id, ct);
                return TypedResults.NoContent();
            })
            .RequireAuthorization(Roles.Admin);
        group.MapGet("/{id:guid}/content", async (Guid id, KnowledgeService knowledge, CancellationToken ct) =>
        {
            var file = await knowledge.GetContentAsync(id, ct);
            return TypedResults.File(file.Content, file.ContentType, file.FileName);
        });
    }
}
