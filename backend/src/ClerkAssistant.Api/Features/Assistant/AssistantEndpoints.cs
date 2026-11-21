using ClerkAssistant.Api.Infrastructure.Ai;
using ClerkAssistant.Api.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ClerkAssistant.Api.Features.Assistant;

public static class AssistantEndpoints
{
    public static IServiceCollection AddAssistantFeature(this IServiceCollection services)
    {
        services.AddOptions<AssistantOptions>()
            .BindConfiguration(AssistantOptions.Section)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.TryAddSingleton<AiGateway>();
        services.AddSingleton(_ => AppGuide.FromEmbeddedResource());
        return services
            .AddScoped<ConversationService>()
            .AddScoped<CaseFactsLoader>()
            .AddScoped<AssistantContextBuilder>()
            .AddScoped<AssistantLookups>()
            .AddScoped<AssistantProposals>()
            .AddScoped<AssistantChatService>()
            .AddScoped<AssistantActionService>();
    }

    public static void MapAssistantEndpoints(this RouteGroupBuilder api)
    {
        var assistant = api.MapGroup("/assistant");

        assistant.MapGet("/conversations", (ConversationService conversations, CancellationToken ct) =>
            conversations.ListAsync(ct));
        assistant.MapGet("/conversations/{id:guid}", (Guid id, ConversationService conversations, CancellationToken ct) =>
            conversations.GetAsync(id, ct));
        assistant.MapDelete("/conversations/{id:guid}", async (Guid id, ConversationService conversations, CancellationToken ct) =>
        {
            await conversations.DeleteAsync(id, ct);
            return TypedResults.NoContent();
        });

        assistant.MapPost("/messages", async (SendAssistantMessage body, AssistantChatService chat, CancellationToken ct) =>
                TypedResults.ServerSentEvents(await chat.StartAsync(body, ct)))
            .RequireRateLimiting(SecurityExtensions.AiRateLimit);

        assistant.MapPost("/actions/{id:guid}/confirm", (Guid id, AssistantActionService actions, CancellationToken ct) =>
            actions.ConfirmAsync(id, ct));
        assistant.MapPost("/actions/{id:guid}/reject", (Guid id, AssistantActionService actions, CancellationToken ct) =>
            actions.RejectAsync(id, ct));
    }
}
