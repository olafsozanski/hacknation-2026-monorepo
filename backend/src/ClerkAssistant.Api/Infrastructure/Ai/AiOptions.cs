namespace ClerkAssistant.Api.Infrastructure.Ai;

public sealed class AiOptions
{
    public const string Section = "Ai";

    public string Provider { get; set; } = "OpenAI";
    public string? Endpoint { get; set; }
    public string? ApiKey { get; set; }
    public string ChatModel { get; set; } = "gpt-5-mini";
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";
    public bool IsLocal { get; set; }
    public int TimeoutSeconds { get; set; } = 120;

    public bool IsConfigured => IsLocal || !string.IsNullOrWhiteSpace(ApiKey);
}
