using ClerkAssistant.Api.Domain;

namespace ClerkAssistant.Api.Features.Audit;

public static class AuditEndpoints
{
    public static IServiceCollection AddAuditFeature(this IServiceCollection services) =>
        services.AddScoped<AuditService>();

    public static void MapAuditEndpoints(this RouteGroupBuilder api) =>
        api.MapGet("/admin/audit", (long? before, string? action, string? userName, int? take, AuditService audit, CancellationToken ct) =>
                audit.ListAsync(before, action, userName, take ?? 100, ct))
            .RequireAuthorization(Roles.Admin);
}
