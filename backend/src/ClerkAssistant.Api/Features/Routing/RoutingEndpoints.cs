using ClerkAssistant.Api.Infrastructure.Ai;
using ClerkAssistant.Api.Infrastructure.Security;
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
        api.MapGet("/requests/{id:guid}/department-suggestions", (Guid id, DepartmentRouter router, CancellationToken ct) =>
                router.SuggestForRequestAsync(id, ct))
            .RequireRateLimiting(SecurityExtensions.AiRateLimit);
}
