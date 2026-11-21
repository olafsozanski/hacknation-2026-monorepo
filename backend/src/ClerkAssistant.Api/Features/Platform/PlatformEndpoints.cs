using ClerkAssistant.Api.Infrastructure.Ai;
using ClerkAssistant.Api.Infrastructure.Common;
using Microsoft.Extensions.Options;
using Riok.Mapperly.Abstractions;

namespace ClerkAssistant.Api.Features.Platform;

public record AiInfo(bool Enabled, bool IsLocal, string Provider, string ChatModel);

public record SystemInfo(string OrganizationName, string UnitName, AiInfo Ai);

[Mapper]
public static partial class PlatformMapper
{
    [MapProperty(nameof(OrganizationOptions.Name), nameof(SystemInfo.OrganizationName))]
    public static partial SystemInfo ToSystemInfo(this OrganizationOptions organization, AiInfo ai);

    public static partial AiInfo ToAiInfo(this AiOptions options, bool enabled);
}

public static class PlatformEndpoints
{
    public static void MapPlatformEndpoints(this RouteGroupBuilder api) =>
        api.MapGet("/system/info", (IOptions<OrganizationOptions> organization, AiClients ai) =>
            organization.Value.ToSystemInfo(ai.Options.ToAiInfo(ai.IsEnabled)));
}
