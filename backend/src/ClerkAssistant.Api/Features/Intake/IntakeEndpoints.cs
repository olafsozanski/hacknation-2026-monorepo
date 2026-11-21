using ClerkAssistant.Api.Infrastructure.Ai;
using ClerkAssistant.Api.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ClerkAssistant.Api.Features.Intake;

public static class IntakeEndpoints
{
    public static IServiceCollection AddIntakeFeature(this IServiceCollection services)
    {
        services.TryAddSingleton<AiGateway>();
        services.AddScoped<IntakeService>();
        services.AddSingleton<UnattachedFileCleanup>();
        services.AddHostedService(sp => sp.GetRequiredService<UnattachedFileCleanup>());
        return services;
    }

    public static void MapIntakeEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/intake").RequireRateLimiting(SecurityExtensions.AiRateLimit);
        group.MapPost("/", (IFormFile? file, IntakeService intake, CancellationToken ct) => intake.AnalyzeFileAsync(file, ct));
        group.MapPost("/text", (IntakeTextRequest request, IntakeService intake, CancellationToken ct) =>
            intake.AnalyzeTextAsync(request, ct));
    }
}
