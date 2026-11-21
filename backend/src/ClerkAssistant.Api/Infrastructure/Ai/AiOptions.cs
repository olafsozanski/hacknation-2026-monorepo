using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.AI;

namespace ClerkAssistant.Api.Infrastructure.Ai;

public sealed class AiOptions
{
    public const string Section = "Ai";

    private static readonly string[] InternalSuffixes = [".local", ".internal", ".lan", ".localdomain"];

    public string Provider { get; set; } = "OpenAI";
    public string? Endpoint { get; set; }
    public string? ApiKey { get; set; }
    public string ChatModel { get; set; } = "gpt-5-mini";
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";
    public bool IsLocal { get; set; }
    public bool AllowExternalProcessing { get; set; }
    public int TimeoutSeconds { get; set; } = 120;
    public float? Temperature { get; set; }
    public int IndexingIntervalSeconds { get; set; } = 30;
    public int IndexingBatchSize { get; set; } = 16;
    public bool AssistantTools { get; set; } = true;
    public ReasoningEffort? AssistantReasoningEffort { get; set; }

    public bool UsesLocalEndpoint => IsLocal && IsInternalEndpoint(Endpoint);

    public bool IsConfigured => UsesLocalEndpoint || (AllowExternalProcessing && !string.IsNullOrWhiteSpace(ApiKey));

    private static bool IsInternalEndpoint(string? endpoint)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (IPAddress.TryParse(uri.Host, out var address))
        {
            return IPAddress.IsLoopback(address) || IsPrivate(address);
        }

        return uri.IsLoopback
            || !uri.Host.Contains('.')
            || InternalSuffixes.Any(suffix => uri.Host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsPrivate(IPAddress address)
    {
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return address.IsIPv6UniqueLocal || address.IsIPv6LinkLocal;
        }

        var b = address.GetAddressBytes();
        return b[0] == 10 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) || (b[0] == 192 && b[1] == 168);
    }
}
