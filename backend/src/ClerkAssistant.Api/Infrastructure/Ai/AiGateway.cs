using System.ClientModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using ClerkAssistant.Api.Infrastructure.Persistence;
using Microsoft.Extensions.AI;
using Pgvector;

namespace ClerkAssistant.Api.Infrastructure.Ai;

public sealed record AiAttempt<T>(T? Value, bool Failed) where T : class
{
    public static AiAttempt<T> Skipped { get; } = new(null, false);

    public bool Succeeded => Value is not null;
}

public enum AiStreamUpdateKind
{
    Text,
    ToolCall,
    Failed,
}

public sealed record AiStreamUpdate(AiStreamUpdateKind Kind, string Value)
{
    public static AiStreamUpdate Failure { get; } = new(AiStreamUpdateKind.Failed, "");
}

public sealed class AiGateway(AiClients ai, ILogger<AiGateway> logger)
{
    public const string FallbackWarning = "Model AI jest niedostępny — użyto trybu podstawowego.";

    public bool IsEnabled => ai.IsEnabled;

    public bool CanUseTools => ai.CanUseTools;

    public bool CanEmbed => ai.IsEnabled && ai.Embeddings is not null;

    public Task<AiAttempt<T>> GetStructuredAsync<T>(
        string operation, string systemPrompt, string userPrompt, CancellationToken ct) where T : class =>
        RunAsync(operation, ai.IsEnabled, async token =>
        {
            var response = await ai.Chat!.GetResponseAsync<T>(
                Messages(systemPrompt, userPrompt), CreateOptions(), cancellationToken: token);
            return response.Result;
        }, ct);

    public Task<AiAttempt<string>> GetTextAsync(
        string operation, string systemPrompt, string userPrompt, CancellationToken ct) =>
        RunAsync(operation, ai.IsEnabled, async token =>
        {
            var response = await ai.Chat!.GetResponseAsync(Messages(systemPrompt, userPrompt), CreateOptions(), token);
            return string.IsNullOrWhiteSpace(response.Text)
                ? throw new InvalidOperationException("The model returned an empty response.")
                : response.Text;
        }, ct);

    public Task<AiAttempt<Vector[]>> EmbedAsync(string operation, IReadOnlyList<string> texts, CancellationToken ct) =>
        RunAsync(operation, CanEmbed && texts.Count > 0, async token =>
        {
            var embeddings = await ai.Embeddings!.GenerateAsync(texts, cancellationToken: token);
            if (embeddings.Count != texts.Count)
            {
                throw new InvalidOperationException("The embedding model returned an unexpected number of vectors.");
            }

            var wrongSize = embeddings.FirstOrDefault(e => e.Vector.Length != AppDbContext.EmbeddingDimensions);
            if (wrongSize is not null)
            {
                logger.LogWarning(
                    "Embedding model returned {Dimensions} dimensions but the database expects {Expected}",
                    wrongSize.Vector.Length,
                    AppDbContext.EmbeddingDimensions);
                throw new InvalidOperationException("The embedding model returned vectors of an unexpected size.");
            }

            return embeddings.Select(e => new Vector(e.Vector)).ToArray();
        }, ct);

    public async IAsyncEnumerable<AiStreamUpdate> StreamChatAsync(
        string operation,
        IReadOnlyList<ChatMessage> messages,
        IList<AITool>? tools,
        [EnumeratorCancellation] CancellationToken ct)
    {
        if (!ai.IsEnabled)
        {
            yield return AiStreamUpdate.Failure;
            yield break;
        }

        var useTools = tools is { Count: > 0 } && ai.CanUseTools;
        var client = useTools ? ai.ToolChat! : ai.Chat!;
        var options = CreateOptions();
        if (useTools)
        {
            options.Tools = tools;
            options.Reasoning = ToolReasoning();
        }
        await using var updates = client.GetStreamingResponseAsync(messages, options, ct).GetAsyncEnumerator(ct);
        while (true)
        {
            ChatResponseUpdate? update = null;
            try
            {
                if (!await updates.MoveNextAsync())
                {
                    yield break;
                }

                update = updates.Current;
            }
            catch (Exception ex) when (IsModelFailure(ex, ct))
            {
                LogFailure(operation, ex);
            }

            if (update is null)
            {
                yield return AiStreamUpdate.Failure;
                yield break;
            }

            foreach (var content in update.Contents)
            {
                switch (content)
                {
                    case FunctionCallContent call:
                        yield return new AiStreamUpdate(AiStreamUpdateKind.ToolCall, call.Name);
                        break;
                    case TextContent { Text.Length: > 0 } text when update.Role != ChatRole.Tool:
                        yield return new AiStreamUpdate(AiStreamUpdateKind.Text, text.Text);
                        break;
                }
            }
        }
    }

    public static bool IsModelFailure(Exception exception, CancellationToken ct) => exception switch
    {
        OperationCanceledException => !ct.IsCancellationRequested,
        HttpRequestException or ClientResultException or JsonException or InvalidOperationException => true,
        _ => false,
    };

    private async Task<AiAttempt<T>> RunAsync<T>(
        string operation, bool available, Func<CancellationToken, Task<T>> call, CancellationToken ct) where T : class
    {
        if (!available)
        {
            return AiAttempt<T>.Skipped;
        }

        try
        {
            return new AiAttempt<T>(await call(ct), false);
        }
        catch (Exception ex) when (IsModelFailure(ex, ct))
        {
            LogFailure(operation, ex);
            return new AiAttempt<T>(null, true);
        }
    }

    private void LogFailure(string operation, Exception ex) =>
        logger.LogWarning(
            "AI operation {Operation} failed ({ErrorType}, status {Status}); using the fallback",
            operation,
            ex.GetType().Name,
            StatusOf(ex));

    private ChatOptions CreateOptions() => new() { Temperature = ai.Options.Temperature };

    private ReasoningOptions? ToolReasoning() =>
        ai.Options.AssistantReasoningEffort is { } effort ? new ReasoningOptions { Effort = effort } : null;

    private static ChatMessage[] Messages(string systemPrompt, string userPrompt) =>
    [
        new(ChatRole.System, systemPrompt),
        new(ChatRole.User, userPrompt),
    ];

    private static int? StatusOf(Exception exception) => exception switch
    {
        ClientResultException e => e.Status,
        HttpRequestException { StatusCode: { } code } => (int)code,
        _ => null,
    };
}
