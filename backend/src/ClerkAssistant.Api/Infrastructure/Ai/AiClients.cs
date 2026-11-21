using System.ClientModel;
using ClerkAssistant.Api.Infrastructure.Persistence;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI;

namespace ClerkAssistant.Api.Infrastructure.Ai;

public sealed class AiClients
{
    public const int MaxToolIterations = 5;

    public AiClients(IOptions<AiOptions> options)
    {
        Options = options.Value;
        if (!Options.IsConfigured)
        {
            return;
        }

        var client = new OpenAIClient(
            new ApiKeyCredential(string.IsNullOrWhiteSpace(Options.ApiKey) ? "local" : Options.ApiKey),
            new OpenAIClientOptions
            {
                Endpoint = string.IsNullOrWhiteSpace(Options.Endpoint) ? null : new Uri(Options.Endpoint),
                NetworkTimeout = TimeSpan.FromSeconds(Options.TimeoutSeconds),
            });
        Chat = client.GetChatClient(Options.ChatModel).AsIChatClient();
        ToolChat = WithToolInvocation(Chat);
        Embeddings = client.GetEmbeddingClient(Options.EmbeddingModel)
            .AsIEmbeddingGenerator(AppDbContext.EmbeddingDimensions);
    }

    internal AiClients(
        AiOptions options,
        IChatClient? chat,
        IEmbeddingGenerator<string, Embedding<float>>? embeddings)
    {
        Options = options;
        Chat = chat;
        ToolChat = chat is null ? null : WithToolInvocation(chat);
        Embeddings = embeddings;
    }

    public AiOptions Options { get; }
    public IChatClient? Chat { get; }
    public IChatClient? ToolChat { get; }
    public IEmbeddingGenerator<string, Embedding<float>>? Embeddings { get; }

    public bool IsEnabled => Chat is not null;

    public bool CanUseTools => IsEnabled && Options.AssistantTools;

    private static FunctionInvokingChatClient WithToolInvocation(IChatClient chat) => new(chat)
    {
        MaximumIterationsPerRequest = MaxToolIterations,
        AllowConcurrentInvocation = false,
        IncludeDetailedErrors = false,
    };
}
