using ClerkAssistant.Api.Features.Assistant;
using ClerkAssistant.Api.Infrastructure.Ai;
using ClerkAssistant.Api.Infrastructure.Common;
using Microsoft.Extensions.Options;
using Riok.Mapperly.Abstractions;

namespace ClerkAssistant.Api.Features.Platform;

public record AiInfo(bool Enabled, bool IsLocal, string Provider, string ChatModel, bool AssistantTools);

public record SystemInfo(string OrganizationName, string UnitName, AiInfo Ai, int AssistantRetentionDays);

[Mapper]
public static partial class PlatformMapper
{
    [MapProperty(nameof(OrganizationOptions.Name), nameof(SystemInfo.OrganizationName))]
    public static partial SystemInfo ToSystemInfo(this OrganizationOptions organization, AiInfo ai, int assistantRetentionDays);

    [MapProperty(nameof(AiOptions.UsesLocalEndpoint), nameof(AiInfo.IsLocal))]
    [MapperIgnoreSource(nameof(AiOptions.AssistantTools))]
    public static partial AiInfo ToAiInfo(this AiOptions options, bool enabled, bool assistantTools);
}

public static class PlatformEndpoints
{
    public static void MapPlatformEndpoints(this RouteGroupBuilder api) =>
        api.MapGet("/system/info", (IOptions<OrganizationOptions> organization, IOptions<AssistantOptions> assistant, AiClients ai) =>
            organization.Value.ToSystemInfo(
                ai.Options.ToAiInfo(ai.IsEnabled, ai.CanUseTools),
                assistant.Value.RetentionDays));
}
