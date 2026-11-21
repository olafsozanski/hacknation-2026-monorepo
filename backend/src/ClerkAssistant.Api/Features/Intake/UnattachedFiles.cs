using ClerkAssistant.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClerkAssistant.Api.Features.Intake;

public static class UnattachedFiles
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

    public static Task<int> DeleteStaleAsync(AppDbContext db, DateTimeOffset now, CancellationToken ct)
    {
        var cutoff = now - Lifetime;
        return db.Files
            .Where(f => f.CreatedAt < cutoff
                        && !db.RequestDocuments.Any(d => d.FileId == f.Id)
                        && !db.KnowledgeDocuments.Any(k => k.FileId == f.Id))
            .ExecuteDeleteAsync(ct);
    }
}

public sealed class UnattachedFileCleanup(IServiceScopeFactory scopes, TimeProvider time, ILogger<UnattachedFileCleanup> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    public async Task<int> RunOnceAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        return await UnattachedFiles.DeleteStaleAsync(
            scope.ServiceProvider.GetRequiredService<AppDbContext>(), time.GetUtcNow(), ct);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                var deleted = await RunOnceAsync(stoppingToken);
                if (deleted > 0)
                {
                    logger.LogInformation("Deleted {Count} unattached uploaded files", deleted);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning("Unattached file cleanup failed ({ErrorType}); it will be retried", ex.GetType().Name);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
