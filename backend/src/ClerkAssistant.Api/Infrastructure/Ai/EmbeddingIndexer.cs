using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ClerkAssistant.Api.Infrastructure.Ai;

public sealed class EmbeddingIndexer(
    IServiceScopeFactory scopes,
    AiGateway ai,
    IOptions<AiOptions> options,
    ILogger<EmbeddingIndexer> logger) : BackgroundService
{
    public const int MaxEmbeddedTextLength = 8000;
    private const int MaxBatchesPerRun = 50;

    private int BatchSize => Math.Clamp(options.Value.IndexingBatchSize, 1, 256);

    public static string RequestText(string subject, string content) => Clip($"{subject}\n{content}");

    public static string Clip(string text) =>
        text.Length > MaxEmbeddedTextLength ? text[..MaxEmbeddedTextLength] : text;

    public async Task<int> IndexPendingAsync(CancellationToken ct)
    {
        if (!ai.CanEmbed)
        {
            return 0;
        }

        var total = 0;
        for (var run = 0; run < MaxBatchesPerRun; run++)
        {
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (await IndexRequestsAsync(db, ct) is not { } requests
                || await IndexChunksAsync(db, ct) is not { } chunks)
            {
                break;
            }

            total += requests + chunks;
            if (requests < BatchSize && chunks < BatchSize)
            {
                break;
            }
        }

        return total;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!ai.CanEmbed)
        {
            return;
        }

        await Task.Yield();
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, options.Value.IndexingIntervalSeconds)));
        do
        {
            try
            {
                var indexed = await IndexPendingAsync(stoppingToken);
                if (indexed > 0)
                {
                    logger.LogInformation("Embedded {Count} requests and knowledge chunks", indexed);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning("Embedding indexing failed ({ErrorType}); it will be retried", ex.GetType().Name);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task<int?> IndexRequestsAsync(AppDbContext db, CancellationToken ct)
    {
        var pending = await db.Requests
            .Where(r => r.Embedding == null)
            .OrderBy(r => r.UpdatedAt)
            .Select(r => new { r.Id, r.Subject, Content = r.Content.Substring(0, MaxEmbeddedTextLength) })
            .Take(BatchSize)
            .ToListAsync(ct);
        if (pending.Count == 0)
        {
            return 0;
        }

        var vectors = (await ai.EmbedAsync("index-requests", pending.Select(p => RequestText(p.Subject, p.Content)).ToList(), ct)).Value;
        if (vectors is null)
        {
            return null;
        }

        var existing = await db.RequestEmbeddings
            .Where(e => pending.Select(p => p.Id).Contains(e.RequestId))
            .Select(e => e.RequestId)
            .ToListAsync(ct);
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < pending.Count; i++)
        {
            if (!existing.Contains(pending[i].Id))
            {
                db.RequestEmbeddings.Add(new RequestEmbedding { RequestId = pending[i].Id, Vector = vectors[i], CreatedAt = now });
            }
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
        }

        return pending.Count;
    }

    private async Task<int?> IndexChunksAsync(AppDbContext db, CancellationToken ct)
    {
        var pending = await db.KnowledgeChunks
            .Where(c => c.Embedding == null)
            .OrderBy(c => c.DocumentId).ThenBy(c => c.Index)
            .Select(c => new { c.Id, c.Text })
            .Take(BatchSize)
            .ToListAsync(ct);
        if (pending.Count == 0)
        {
            return 0;
        }

        var vectors = (await ai.EmbedAsync("index-knowledge", pending.Select(p => p.Text).ToList(), ct)).Value;
        if (vectors is null)
        {
            return null;
        }

        for (var i = 0; i < pending.Count; i++)
        {
            var id = pending[i].Id;
            var vector = vectors[i];
            await db.KnowledgeChunks
                .Where(c => c.Id == id && c.Embedding == null)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.Embedding, vector), ct);
        }

        return pending.Count;
    }
}
