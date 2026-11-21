using ClerkAssistant.Api.Infrastructure.Ai;
using ClerkAssistant.Api.Infrastructure.Common;
using Microsoft.Extensions.Options;

namespace ClerkAssistant.Api.Features.Platform;

public record AiInfo(bool Enabled, bool IsLocal, string Provider, string ChatModel);

public record SystemInfo(string OrganizationName, string UnitName, AiInfo Ai);

public static class PlatformEndpoints
{
    public static void MapPlatformEndpoints(this RouteGroupBuilder api) =>
        api.MapGet("/system/info", (IOptions<OrganizationOptions> organization, AiClients ai) => new SystemInfo(
            organization.Value.Name,
            organization.Value.UnitName,
            new AiInfo(ai.IsEnabled, ai.Options.IsLocal, ai.Options.Provider, ai.Options.ChatModel)));
}
