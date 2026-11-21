using ClerkAssistant.Api.Infrastructure.Security;

namespace ClerkAssistant.Api.Features.Drafts;

public static class DraftsEndpoints
{
    public static IServiceCollection AddDraftsFeature(this IServiceCollection services) =>
        services
            .AddScoped<DraftComposer>()
            .AddScoped<DraftService>();

    public static void MapDraftsEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/requests/{id:guid}/draft");
        group.MapGet("/", (Guid id, DraftService drafts, CancellationToken ct) => drafts.GetAsync(id, ct));
        group.MapPut("/", (Guid id, SaveDraftRequest request, DraftService drafts, CancellationToken ct) =>
            drafts.SaveAsync(id, request, ct));
        group.MapPost("/generate", (Guid id, uint? version, DraftService drafts, CancellationToken ct) =>
                drafts.GenerateAsync(id, version, ct))
            .RequireRateLimiting(SecurityExtensions.AiRateLimit);
        group.MapPost("/detect", (Guid id, DetectRedactionsRequest request, DraftService drafts, CancellationToken ct) =>
                drafts.DetectAsync(id, request, ct))
            .RequireRateLimiting(SecurityExtensions.AiRateLimit);
        group.MapGet("/export", async (Guid id, string? format, DraftService drafts, CancellationToken ct) =>
        {
            var file = await drafts.ExportAsync(id, format, ct);
            return TypedResults.File(file.Content, file.ContentType, file.FileName);
        });
    }
}
