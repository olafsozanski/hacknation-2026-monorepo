using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Infrastructure.Ai;
using ClerkAssistant.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace ClerkAssistant.Api.Features.Search;

public sealed record QueryEmbedding(Vector? Vector, bool Failed)
{
    public static QueryEmbedding None { get; } = new(null, false);
}

public sealed record SearchRequest(string Text)
{
    public bool IncludeRequests { get; init; } = true;
    public bool IncludeKnowledge { get; init; } = true;
    public DateOnly? RequestsReceivedSince { get; init; }
    public IReadOnlyCollection<KnowledgeKind>? KnowledgeKinds { get; init; }
    public Guid? ExcludeRequestId { get; init; }
    public int Limit { get; init; } = 20;
    public QueryEmbedding? Embedding { get; init; }
}

public sealed record SearchOutcome(SearchHit[] Hits, bool AiFailed);

public sealed record KnowledgePassage(Guid DocumentId, string Title, KnowledgeKind Kind, string Text);

public sealed class HybridSearch(AppDbContext db, AiGateway ai)
{
    private const string Config = AppDbContext.TextSearchConfig;
    private const double MinTrigramSimilarity = 0.3;
    private const double MaxCosineDistance = 0.75;
    private const int SnippetSourceLength = 4000;
    private static readonly TimeZoneInfo Warsaw = TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw");

    public async Task<QueryEmbedding> EmbedAsync(string text, CancellationToken ct)
    {
        var attempt = await ai.EmbedAsync("embed-query", [EmbeddingIndexer.Clip(text)], ct);
        return new QueryEmbedding(attempt.Value?[0], attempt.Failed);
    }

    public async Task<SearchOutcome> SearchAsync(SearchRequest request, CancellationToken ct)
    {
        var (ranked, plan, aiFailed) = await RankAsync(request, ct);
        return new SearchOutcome(await LoadHitsAsync(ranked, plan, ct), aiFailed);
    }

    public async Task<(KnowledgePassage[] Passages, bool AiFailed)> FindPassagesAsync(
        SearchRequest request, CancellationToken ct)
    {
        var (ranked, _, aiFailed) = await RankAsync(request with { IncludeRequests = false }, ct);
        var chunkIds = ranked.Select(r => r.ChunkId!.Value).ToList();
        var chunks = await db.KnowledgeChunks
            .Where(c => chunkIds.Contains(c.Id))
            .Select(c => new { c.Id, c.DocumentId, c.Document.Title, c.Document.Kind, c.Text })
            .ToDictionaryAsync(c => c.Id, ct);
        var passages = chunkIds
            .Where(chunks.ContainsKey)
            .Select(id => chunks[id])
            .Select(c => new KnowledgePassage(c.DocumentId, c.Title, c.Kind, c.Text))
            .ToArray();
        return (passages, aiFailed);
    }

    private async Task<(IReadOnlyList<FusedHit> Ranked, QueryPlan Plan, bool AiFailed)> RankAsync(
        SearchRequest request, CancellationToken ct)
    {
        var plan = QueryPlan.From(request.Text);
        var embedding = request.Embedding ?? await EmbedAsync(request.Text, ct);
        var fusion = new RankFusion();
        var take = Math.Max(request.Limit * 3, 30);
        if (request.IncludeRequests)
        {
            await AddRequestCandidatesAsync(fusion, request, plan, embedding.Vector, take, ct);
        }

        if (request.IncludeKnowledge)
        {
            await AddKnowledgeCandidatesAsync(fusion, request, plan, embedding.Vector, take, ct);
        }

        return (fusion.Top(request.Limit), plan, embedding.Failed);
    }

    private async Task AddRequestCandidatesAsync(
        RankFusion fusion, SearchRequest request, QueryPlan plan, Vector? vector, int take, CancellationToken ct)
    {
        var requests = db.Requests.AsQueryable();
        if (request.RequestsReceivedSince is { } since)
        {
            requests = requests.Where(r => r.ReceivedOn >= since);
        }

        if (request.ExcludeRequestId is { } excluded)
        {
            requests = requests.Where(r => r.Id != excluded);
        }

        if (plan.TokenQuery is { } tokenQuery)
        {
            fusion.Add(SearchHitKind.Request, await requests
                .Where(r => r.SearchVector.Matches(EF.Functions.ToTsQuery(Config, tokenQuery)))
                .OrderByDescending(r => r.SearchVector.RankCoverDensity(EF.Functions.ToTsQuery(Config, tokenQuery)))
                .ThenByDescending(r => r.ReceivedOn)
                .Select(r => new RankedItem(r.Id, null))
                .Take(take)
                .ToListAsync(ct));
        }

        if (plan.IsShort)
        {
            var text = plan.Text;
            fusion.Add(SearchHitKind.Request, await requests
                .Where(r => r.SearchVector.Matches(EF.Functions.WebSearchToTsQuery(Config, text)))
                .OrderByDescending(r => r.SearchVector.RankCoverDensity(EF.Functions.WebSearchToTsQuery(Config, text)))
                .ThenByDescending(r => r.ReceivedOn)
                .Select(r => new RankedItem(r.Id, null))
                .Take(take)
                .ToListAsync(ct));

            fusion.Add(SearchHitKind.Request, await requests
                .Select(r => new { r.Id, Similarity = EF.Functions.TrigramsWordSimilarity(text, r.Subject) })
                .Where(x => x.Similarity >= MinTrigramSimilarity)
                .OrderByDescending(x => x.Similarity)
                .Select(x => new RankedItem(x.Id, null))
                .Take(take)
                .ToListAsync(ct));
        }

        if (vector is not null)
        {
            fusion.Add(SearchHitKind.Request, await requests
                .Where(r => r.Embedding != null)
                .Select(r => new { r.Id, Distance = r.Embedding!.Vector.CosineDistance(vector) })
                .Where(x => x.Distance <= MaxCosineDistance)
                .OrderBy(x => x.Distance)
                .Select(x => new RankedItem(x.Id, null))
                .Take(take)
                .ToListAsync(ct));
        }
    }

    private async Task AddKnowledgeCandidatesAsync(
        RankFusion fusion, SearchRequest request, QueryPlan plan, Vector? vector, int take, CancellationToken ct)
    {
        var chunks = db.KnowledgeChunks.AsQueryable();
        if (request.KnowledgeKinds is { Count: > 0 })
        {
            var kinds = request.KnowledgeKinds.ToArray();
            chunks = chunks.Where(c => kinds.Contains(c.Document.Kind));
        }

        var chunkTake = take * 3;
        if (plan.TokenQuery is { } tokenQuery)
        {
            fusion.Add(SearchHitKind.Knowledge, BestChunkPerDocument(await chunks
                .Where(c => c.SearchVector.Matches(EF.Functions.ToTsQuery(Config, tokenQuery)))
                .OrderByDescending(c => c.SearchVector.RankCoverDensity(EF.Functions.ToTsQuery(Config, tokenQuery)))
                .Select(c => new RankedItem(c.DocumentId, c.Id))
                .Take(chunkTake)
                .ToListAsync(ct)));
        }

        if (plan.IsShort)
        {
            var text = plan.Text;
            fusion.Add(SearchHitKind.Knowledge, BestChunkPerDocument(await chunks
                .Where(c => c.SearchVector.Matches(EF.Functions.WebSearchToTsQuery(Config, text)))
                .OrderByDescending(c => c.SearchVector.RankCoverDensity(EF.Functions.WebSearchToTsQuery(Config, text)))
                .Select(c => new RankedItem(c.DocumentId, c.Id))
                .Take(chunkTake)
                .ToListAsync(ct)));
        }

        if (vector is not null)
        {
            fusion.Add(SearchHitKind.Knowledge, BestChunkPerDocument(await chunks
                .Where(c => c.Embedding != null)
                .Select(c => new { c.Id, c.DocumentId, Distance = c.Embedding!.CosineDistance(vector) })
                .Where(x => x.Distance <= MaxCosineDistance)
                .OrderBy(x => x.Distance)
                .Select(x => new RankedItem(x.DocumentId, x.Id))
                .Take(chunkTake)
                .ToListAsync(ct)));
        }
    }

    private async Task<SearchHit[]> LoadHitsAsync(IReadOnlyList<FusedHit> ranked, QueryPlan plan, CancellationToken ct)
    {
        var requestIds = ranked.Where(r => r.Kind == SearchHitKind.Request).Select(r => r.Id).ToList();
        var chunkIds = ranked.Where(r => r.Kind == SearchHitKind.Knowledge).Select(r => r.ChunkId!.Value).ToList();

        var requests = requestIds.Count == 0
            ? []
            : await db.Requests
                .Where(r => requestIds.Contains(r.Id))
                .Select(r => new RequestRow(
                    r.Id,
                    r.Subject,
                    r.CaseNumber,
                    r.ReceivedOn,
                    r.Status,
                    r.Content.Substring(0, SnippetSourceLength),
                    r.Draft != null ? r.Draft.Content.Substring(0, SnippetSourceLength) : null,
                    r.Departments.OrderBy(d => d.Department.Symbol).Select(d => d.Department.Symbol).ToArray()))
                .ToDictionaryAsync(r => r.Id, ct);

        var knowledge = chunkIds.Count == 0
            ? []
            : await db.KnowledgeChunks
                .Where(c => chunkIds.Contains(c.Id))
                .Select(c => new KnowledgeRow(c.DocumentId, c.Document.Title, c.Document.Kind, c.Document.CreatedAt, c.Text))
                .ToDictionaryAsync(c => c.DocumentId, ct);

        var hits = new List<SearchHit>(ranked.Count);
        foreach (var item in ranked)
        {
            if (item.Kind == SearchHitKind.Request && requests.TryGetValue(item.Id, out var request))
            {
                hits.Add(ToHit(request, item.Score, plan.Stems));
            }
            else if (item.Kind == SearchHitKind.Knowledge && knowledge.TryGetValue(item.Id, out var document))
            {
                hits.Add(ToHit(document, item.Score, plan.Stems));
            }
        }

        return hits.OrderByDescending(h => h.Score).ThenByDescending(h => h.Date).ToArray();
    }

    private static SearchHit ToHit(RequestRow row, double score, IReadOnlyList<string> stems) => new(
        SearchHitKind.Request,
        row.Id,
        row.Subject,
        row.CaseNumber,
        row.ReceivedOn,
        SearchText.Snippet(SnippetSource(row), stems),
        score,
        row.Status,
        row.DepartmentSymbols,
        null);

    private static SearchHit ToHit(KnowledgeRow row, double score, IReadOnlyList<string> stems) => new(
        SearchHitKind.Knowledge,
        row.DocumentId,
        row.Title,
        null,
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(row.CreatedAt, Warsaw).DateTime),
        SearchText.Snippet(row.Text, stems),
        score,
        null,
        [],
        row.Kind);

    private static string SnippetSource(RequestRow row)
    {
        if (row.Status == RequestStatus.ResponseSent && !string.IsNullOrWhiteSpace(row.DraftContent))
        {
            var body = row.DraftContent.IndexOf("W odpowiedzi", StringComparison.Ordinal);
            return body > 0 ? row.DraftContent[body..] : row.DraftContent;
        }

        return string.IsNullOrWhiteSpace(row.Content) ? row.Subject : row.Content;
    }

    private static List<RankedItem> BestChunkPerDocument(List<RankedItem> chunks) =>
        chunks.DistinctBy(c => c.Id).ToList();

    private sealed record RankedItem(Guid Id, Guid? ChunkId);

    private sealed record FusedHit(SearchHitKind Kind, Guid Id, Guid? ChunkId, double Score);

    private sealed record RequestRow(
        Guid Id,
        string Subject,
        string CaseNumber,
        DateOnly ReceivedOn,
        RequestStatus Status,
        string Content,
        string? DraftContent,
        string[] DepartmentSymbols);

    private sealed record KnowledgeRow(
        Guid DocumentId, string Title, KnowledgeKind Kind, DateTimeOffset CreatedAt, string Text);

    private sealed record QueryPlan(string Text, bool IsShort, string? TokenQuery, IReadOnlyList<string> Stems)
    {
        public static QueryPlan From(string text)
        {
            var isShort = SearchText.IsShortQuery(text);
            var tokens = SearchText.DistinctiveTokens(text, isShort ? 3 : 4);
            return new QueryPlan(text, isShort, SearchText.BuildOrQuery(tokens), SearchText.Stems(tokens));
        }
    }

    private sealed class RankFusion
    {
        private const int K = 60;
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
                    entry = new Entry(kind, item.Id);
                    _entries[(kind, item.Id)] = entry;
                }

                entry.Score += contribution;
                if (item.ChunkId is { } chunkId && contribution > entry.BestChunkContribution)
                {
                    entry.BestChunkId = chunkId;
                    entry.BestChunkContribution = contribution;
                }
            }
        }

        public IReadOnlyList<FusedHit> Top(int limit) => _entries.Values
            .Select(e => new FusedHit(e.Kind, e.Id, e.BestChunkId, Normalize(e)))
            .OrderByDescending(h => h.Score)
            .Take(limit)
            .ToList();

        private double Normalize(Entry entry)
        {
            var best = _lists[entry.Kind] / (double)(K + 1);
            return Math.Round(Math.Min(1.0, entry.Score / best), 3);
        }

        private sealed class Entry(SearchHitKind kind, Guid id)
        {
            public SearchHitKind Kind { get; } = kind;
            public Guid Id { get; } = id;
            public double Score { get; set; }
            public Guid? BestChunkId { get; set; }
            public double BestChunkContribution { get; set; }
        }
    }
}
