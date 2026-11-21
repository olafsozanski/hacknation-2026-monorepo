namespace ClerkAssistant.Api.Features.Requests;

public static class RequestsEndpoints
{
    public static IServiceCollection AddRequestsFeature(this IServiceCollection services) =>
        services.AddScoped<CaseNumberGenerator>();

    public static void MapRequestsEndpoints(this RouteGroupBuilder api)
    {
    }
}
