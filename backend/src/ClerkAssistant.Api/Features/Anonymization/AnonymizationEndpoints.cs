using ClerkAssistant.Api.Infrastructure.Ai;
using ClerkAssistant.Api.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ClerkAssistant.Api.Features.Anonymization;

public static class AnonymizationEndpoints
{
    public static IServiceCollection AddAnonymizationFeature(this IServiceCollection services)
    {
        services.TryAddSingleton<AiGateway>();
        services.TryAddSingleton<PublicContactFilter>();
        services.TryAddSingleton<RedactionDetector>();
        services.TryAddScoped<AnonymizationService>();
        return services;
    }

    public static void MapAnonymizationEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/anonymization");
        group.MapPost("/analyze", (IFormFile? file, AnonymizationService anonymization, CancellationToken ct) =>
                anonymization.AnalyzeFileAsync(file, ct))
            .RequireRateLimiting(SecurityExtensions.AiRateLimit);
        group.MapPost("/analyze-text", (AnalyzeTextRequest request, AnonymizationService anonymization, CancellationToken ct) =>
                anonymization.AnalyzeTextAsync(request, ct))
            .RequireRateLimiting(SecurityExtensions.AiRateLimit);
        group.MapPost("/export", async (AnonymizationExportRequest request, AnonymizationService anonymization, CancellationToken ct) =>
        {
            var file = await anonymization.ExportAsync(request, ct);
            return TypedResults.File(file.Content, file.ContentType, file.FileName);
        });
    }
}
