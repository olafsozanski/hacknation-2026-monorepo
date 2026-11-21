using ClerkAssistant.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClerkAssistant.Api.Features.Search;

public enum SearchCorpus
{
    Requests,
    KnowledgeChunks,
}

public sealed class CorpusSnapshot(int documents, string[] words, int[] documentCounts, DateTimeOffset loadedAt)
{
    public int Documents { get; } = documents;

    public DateTimeOffset LoadedAt { get; } = loadedAt;

    public int DocumentFrequency(string term, bool prefix)
    {
        var index = Array.BinarySearch(words, term, StringComparer.Ordinal);
        if (!prefix)
        {
            return index >= 0 ? documentCounts[index] : 0;
        }

        var total = 0L;
        for (var i = index >= 0 ? index : ~index; i < words.Length && words[i].StartsWith(term, StringComparison.Ordinal); i++)
        {
            total += documentCounts[i];
        }

        return (int)Math.Min(total, Documents);
    }

    public double Share(string term, bool prefix) =>
        Documents == 0 ? 0 : DocumentFrequency(term, prefix) / (double)Documents;

    public double InverseDocumentFrequency(string term, bool prefix)
    {
        var frequency = DocumentFrequency(term, prefix);
        return Math.Log(1 + (Documents - frequency + 0.5) / (frequency + 0.5));
    }
}

public sealed class TermStatistics(TimeProvider time)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    private static readonly Dictionary<SearchCorpus, string> Queries = new()
    {
        [SearchCorpus.Requests] = "SELECT word, ndoc FROM ts_stat('SELECT search_vector FROM requests')",
        [SearchCorpus.KnowledgeChunks] = "SELECT word, ndoc FROM ts_stat('SELECT search_vector FROM knowledge_chunks')",
    };

    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly Dictionary<SearchCorpus, CorpusSnapshot> _snapshots = [];

    public async Task<CorpusSnapshot> GetAsync(AppDbContext db, SearchCorpus corpus, CancellationToken ct)
    {
        var documents = corpus == SearchCorpus.Requests
            ? await db.Requests.CountAsync(ct)
            : await db.KnowledgeChunks.CountAsync(ct);
        await _lock.WaitAsync(ct);
        try
        {
            if (_snapshots.TryGetValue(corpus, out var cached)
                && cached.Documents == documents
                && time.GetUtcNow() - cached.LoadedAt < Lifetime)
            {
                return cached;
            }

            var snapshot = await LoadAsync(db, corpus, documents, ct);
            _snapshots[corpus] = snapshot;
            return snapshot;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<CorpusSnapshot> LoadAsync(AppDbContext db, SearchCorpus corpus, int documents, CancellationToken ct)
    {
        var entries = new List<(string Word, int Count)>();
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = Queries[corpus];
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                entries.Add((reader.GetString(0), reader.GetInt32(1)));
            }
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }

        entries.Sort((a, b) => string.CompareOrdinal(a.Word, b.Word));
        return new CorpusSnapshot(
            documents, entries.Select(e => e.Word).ToArray(), entries.Select(e => e.Count).ToArray(), time.GetUtcNow());
    }
}
