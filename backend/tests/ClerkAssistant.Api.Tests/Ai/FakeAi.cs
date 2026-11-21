using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using ClerkAssistant.Api.Infrastructure.Ai;
using ClerkAssistant.Api.Infrastructure.Persistence;
using ClerkAssistant.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace ClerkAssistant.Api.Tests.Ai;

public sealed record ChatCall(string SystemPrompt, string UserPrompt);

public sealed record StreamCall(IReadOnlyList<ChatMessage> Messages, ChatOptions? Options)
{
    public string SystemPrompt => Messages.First(m => m.Role == ChatRole.System).Text;

    public IEnumerable<string> ToolResults => Messages
        .SelectMany(m => m.Contents.OfType<FunctionResultContent>())
        .Select(r => r.Result?.ToString() ?? "");
}

public sealed class FakeChatClient : IChatClient
{
    public ConcurrentQueue<ChatCall> Calls { get; } = new();

    public Func<ChatCall, string> Responder { get; set; } = _ => "{}";

    public ConcurrentQueue<StreamCall> StreamCalls { get; } = new();

    public Func<IReadOnlyList<ChatMessage>, ChatOptions?, IEnumerable<ChatResponseUpdate>>? Streamer { get; set; }

    public static ChatResponseUpdate Hang { get; } = new(ChatRole.Assistant, "");

    public static ChatResponseUpdate Text(string text) => new(ChatRole.Assistant, text);

    public static ChatResponseUpdate ToolCall(string name, Dictionary<string, object?> arguments) =>
        new(ChatRole.Assistant, [new FunctionCallContent(Guid.NewGuid().ToString("N"), name, arguments)]);

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var list = messages.ToList();
        var call = new ChatCall(
            list.FirstOrDefault(m => m.Role == ChatRole.System)?.Text ?? "",
            list.LastOrDefault(m => m.Role == ChatRole.User)?.Text ?? "");
        Calls.Enqueue(call);
        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, Responder(call))));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (Streamer is null)
        {
            var response = await GetResponseAsync(messages, options, cancellationToken);
            yield return new ChatResponseUpdate(ChatRole.Assistant, response.Text);
            yield break;
        }

        var history = messages.ToList();
        StreamCalls.Enqueue(new StreamCall(history, options));
        foreach (var update in Streamer(history, options))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            if (ReferenceEquals(update, Hang))
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }

            yield return update;
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}

public sealed class FakeEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new GeneratedEmbeddings<Embedding<float>>(values.Select(v => new Embedding<float>(Embed(v)))));
    }

    private static readonly Dictionary<string, string> Synonyms = new()
    {
        ["rodo"] = "ochrona danych osobowych",
    };

    public static float[] Embed(string text)
    {
        var vector = new float[AppDbContext.EmbeddingDimensions];
        var expanded = Synonyms.Aggregate(text, (current, s) =>
            current.Contains(s.Key, StringComparison.OrdinalIgnoreCase) ? $"{current} {s.Value}" : current);
        var words = expanded.ToLowerInvariant()
            .Split(expanded.Where(c => !char.IsLetterOrDigit(c)).Distinct().ToArray(), StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 4);
        foreach (var word in words)
        {
            var stem = word.Length > 5 ? word[..5] : word;
            vector[(int)(Fnv(stem) % (uint)vector.Length)] += 1;
        }

        vector[0] += 0.01f;
        var norm = MathF.Sqrt(vector.Sum(x => x * x));
        return vector.Select(x => x / norm).ToArray();
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }

    private static uint Fnv(string value)
    {
        var hash = 2166136261u;
        foreach (var c in value)
        {
            hash = (hash ^ c) * 16777619u;
        }

        return hash;
    }
}

public class FakeAiFactory : AiApiFactory
{
    public FakeChatClient Chat { get; } = new();

    public FakeEmbeddingGenerator Embeddings { get; } = new();

    protected override IReadOnlyDictionary<string, string?> Settings => new Dictionary<string, string?>
    {
        ["Ai:IndexingIntervalSeconds"] = "3600",
    };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services => services.Replace(ServiceDescriptor.Singleton(sp =>
            new AiClients(sp.GetRequiredService<IOptions<AiOptions>>().Value, Chat, Embeddings))));
    }
}
