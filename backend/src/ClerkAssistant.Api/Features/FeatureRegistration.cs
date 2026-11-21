using ClerkAssistant.Api.Features.Anonymization;
using ClerkAssistant.Api.Features.Auth;
using ClerkAssistant.Api.Features.Departments;
using ClerkAssistant.Api.Features.Documents;
using ClerkAssistant.Api.Features.Drafts;
using ClerkAssistant.Api.Features.Intake;
using ClerkAssistant.Api.Features.Knowledge;
using ClerkAssistant.Api.Features.Platform;
using ClerkAssistant.Api.Features.Requests;
using ClerkAssistant.Api.Features.Routing;
using ClerkAssistant.Api.Features.Search;
using ClerkAssistant.Api.Features.Users;
using ClerkAssistant.Api.Infrastructure.Security;

namespace ClerkAssistant.Api.Features;

public static class FeatureRegistration
{
    public static IServiceCollection AddFeatures(this IServiceCollection services) =>
        services
            .AddAuthFeature()
            .AddUsersFeature()
            .AddRequestsFeature()
            .AddDocumentsFeature()
            .AddDepartmentsFeature()
            .AddIntakeFeature()
            .AddSearchFeature()
            .AddRoutingFeature()
            .AddAnonymizationFeature()
            .AddDraftsFeature()
            .AddKnowledgeFeature();

    public static void MapFeatures(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api")
            .RequireAuthorization()
            .AddEndpointFilter<AntiforgeryFilter>();

        api.MapAuthEndpoints();
        api.MapUsersEndpoints();
        api.MapPlatformEndpoints();
        api.MapRequestsEndpoints();
        api.MapDocumentsEndpoints();
        api.MapDepartmentsEndpoints();
        api.MapIntakeEndpoints();
        api.MapSearchEndpoints();
        api.MapRoutingEndpoints();
        api.MapDraftsEndpoints();
        api.MapAnonymizationEndpoints();
        api.MapKnowledgeEndpoints();
        api.MapFallback(() => TypedResults.NotFound()).AllowAnonymous();
    }
}
