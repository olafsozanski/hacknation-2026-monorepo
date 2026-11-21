namespace ClerkAssistant.Api.Features.Search;

internal sealed record RankedItem(Guid Id, Guid? ChunkId, DateOnly? Date, double Signal);

internal sealed record FusedHit(SearchHitKind Kind, Guid Id, Guid? ChunkId, double Score);

internal sealed class RankFusion(DateOnly? preferSince)
{
    private const int K = 60;
    private const double OlderCaseFactor = 0.9;
    private const double RankShare = 0.25;

    private readonly Dictionary<(SearchHitKind, Guid), Entry> _entries = [];
    private readonly Dictionary<SearchHitKind, int> _lists = [];

    public void Add(SearchHitKind kind, IReadOnlyList<RankedItem> ranked)
    {
        if (ranked.Count == 0)
        {
            return;
        }

        _lists[kind] = _lists.GetValueOrDefault(kind) + 1;
        for (var rank = 0; rank < ranked.Count; rank++)
        {
            var item = ranked[rank];
            var contribution = 1.0 / (K + rank + 1);
            if (!_entries.TryGetValue((kind, item.Id), out var entry))
            {
                entry = new Entry(kind, item.Id, item.Date);
                _entries[(kind, item.Id)] = entry;
            }

            entry.Fused += contribution;
            entry.Signal = Math.Max(entry.Signal, item.Signal);
            if (item.ChunkId is { } chunkId && contribution > entry.BestChunkContribution)
            {
                entry.BestChunkId = chunkId;
                entry.BestChunkContribution = contribution;
            }
        }
    }

    public IReadOnlyList<FusedHit> Top(int limit) => _entries.Values
        .Select(e => new FusedHit(e.Kind, e.Id, e.BestChunkId, Score(e)))
        .OrderByDescending(h => h.Score)
        .Take(limit)
        .ToList();

    private double Score(Entry entry)
    {
        var rank = Math.Min(1.0, entry.Fused / (_lists[entry.Kind] / (double)(K + 1)));
        var recency = entry.Date < preferSince ? OlderCaseFactor : 1.0;
        return Math.Round(Math.Clamp(entry.Signal, 0, 1) * (1 - RankShare + RankShare * rank) * recency, 3);
    }

    private sealed class Entry(SearchHitKind kind, Guid id, DateOnly? date)
    {
        public SearchHitKind Kind { get; } = kind;
        public Guid Id { get; } = id;
        public DateOnly? Date { get; } = date;
        public double Fused { get; set; }
        public double Signal { get; set; }
        public Guid? BestChunkId { get; set; }
        public double BestChunkContribution { get; set; }
    }
}
