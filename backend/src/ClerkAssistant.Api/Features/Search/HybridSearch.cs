using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Infrastructure.Ai;
using ClerkAssistant.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace ClerkAssistant.Api.Features.Search;

public sealed class HybridSearch(AppDbContext db, AiGateway ai, TermStatistics statistics)
{
    private const string Config = AppDbContext.TextSearchConfig;
    private const double MinTrigramSimilarity = 0.3;
    private const double MaxCosineDistance = 0.75;
    private const double MinTermSignal = 0.2;
    private const double PhraseMatchSignal = 0.8;
    private const int SnippetSourceLength = 4000;
    private const int RequestCandidatePool = 200;
    private const int ChunkCandidatePool = 300;
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
            .Select(c => new KnowledgePassage(c.DocumentId, c.Document.Title, c.Document.Kind, c.Text))
            .ToListAsync(ct);
        var order = ranked.Select((r, i) => (r.Id, i)).ToDictionary(x => x.Id, x => x.i);
        return (chunks.OrderBy(c => order[c.DocumentId]).ToArray(), aiFailed);
    }

    internal static double CoverageSignal(double coverage) => Math.Sqrt(Math.Clamp(coverage, 0, 1));

    internal static double CosineSignal(double distance) => Math.Clamp((1 - distance - 0.2) / 0.6, 0, 1);

    private async Task<(IReadOnlyList<FusedHit> Ranked, QueryPlan Plan, bool AiFailed)> RankAsync(
        SearchRequest request, CancellationToken ct)
    {
        var plan = QueryPlan.From(request.Text);
        var embedding = request.Embedding ?? await EmbedAsync(request.Text, ct);
        var fusion = new RankFusion(request.PreferReceivedSince);
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

        var terms = TermQuery.Build(
            plan.Tokens, await statistics.GetAsync(db, SearchCorpus.Requests, ct), dropCommonTerms: !plan.IsShort);
        if (!terms.IsEmpty)
        {
            var tsQuery = terms.TsQuery;
            var candidates = await requests
                .Where(r => r.SearchVector.Matches(EF.Functions.ToTsQuery(Config, tsQuery)))
                .OrderByDescending(r => r.SearchVector.RankCoverDensity(EF.Functions.ToTsQuery(Config, tsQuery)))
                .Take(RequestCandidatePool)
                .Select(r => new { r.Id, r.ReceivedOn, r.SearchVector })
                .ToListAsync(ct);
            fusion.Add(SearchHitKind.Request, candidates
                .Select(c => new RankedItem(c.Id, null, c.ReceivedOn, CoverageSignal(terms.Coverage(c.SearchVector))))
                .Where(i => i.Signal >= MinTermSignal)
                .OrderByDescending(i => i.Signal)
                .ThenByDescending(i => i.Date)
                .Take(take)
                .ToList());
        }

        if (plan.IsShort)
        {
            var text = plan.Text;
            fusion.Add(SearchHitKind.Request, await requests
                .Where(r => r.SearchVector.Matches(EF.Functions.WebSearchToTsQuery(Config, text)))
                .OrderByDescending(r => r.SearchVector.RankCoverDensity(EF.Functions.WebSearchToTsQuery(Config, text)))
                .ThenByDescending(r => r.ReceivedOn)
                .Select(r => new RankedItem(r.Id, null, r.ReceivedOn, PhraseMatchSignal))
                .Take(take)
                .ToListAsync(ct));

            fusion.Add(SearchHitKind.Request, await requests
                .Select(r => new { r.Id, r.ReceivedOn, Similarity = EF.Functions.TrigramsWordSimilarity(text, r.Subject) })
                .Where(x => x.Similarity >= MinTrigramSimilarity)
                .OrderByDescending(x => x.Similarity)
                .Select(x => new RankedItem(x.Id, null, x.ReceivedOn, x.Similarity))
                .Take(take)
                .ToListAsync(ct));
        }

        if (vector is not null)
        {
            var nearest = await requests
                .Where(r => r.Embedding != null)
                .Select(r => new { r.Id, r.ReceivedOn, Distance = r.Embedding!.Vector.CosineDistance(vector) })
                .Where(x => x.Distance <= MaxCosineDistance)
                .OrderBy(x => x.Distance)
                .Take(take)
                .ToListAsync(ct);
            fusion.Add(SearchHitKind.Request,
                nearest.Select(x => new RankedItem(x.Id, null, x.ReceivedOn, CosineSignal(x.Distance))).ToList());
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

        var terms = TermQuery.Build(
            plan.Tokens, await statistics.GetAsync(db, SearchCorpus.KnowledgeChunks, ct), dropCommonTerms: !plan.IsShort);
        if (!terms.IsEmpty)
        {
            var tsQuery = terms.TsQuery;
            var candidates = await chunks
                .Where(c => c.SearchVector.Matches(EF.Functions.ToTsQuery(Config, tsQuery)))
                .OrderByDescending(c => c.SearchVector.RankCoverDensity(EF.Functions.ToTsQuery(Config, tsQuery)))
                .Take(ChunkCandidatePool)
                .Select(c => new { c.Id, c.DocumentId, c.SearchVector })
                .ToListAsync(ct);
            fusion.Add(SearchHitKind.Knowledge, BestChunkPerDocument(candidates
                .Select(c => new RankedItem(c.DocumentId, c.Id, null, CoverageSignal(terms.Coverage(c.SearchVector))))
                .Where(i => i.Signal >= MinTermSignal), take));
        }

        if (plan.IsShort)
        {
            var text = plan.Text;
            fusion.Add(SearchHitKind.Knowledge, BestChunkPerDocument(await chunks
                .Where(c => c.SearchVector.Matches(EF.Functions.WebSearchToTsQuery(Config, text)))
                .OrderByDescending(c => c.SearchVector.RankCoverDensity(EF.Functions.WebSearchToTsQuery(Config, text)))
                .Select(c => new RankedItem(c.DocumentId, c.Id, null, PhraseMatchSignal))
                .Take(take * 3)
                .ToListAsync(ct), take));
        }

        if (vector is not null)
        {
            var nearest = await chunks
                .Where(c => c.Embedding != null)
                .Select(c => new { c.Id, c.DocumentId, Distance = c.Embedding!.CosineDistance(vector) })
                .Where(x => x.Distance <= MaxCosineDistance)
                .OrderBy(x => x.Distance)
                .Take(take * 3)
                .ToListAsync(ct);
            fusion.Add(SearchHitKind.Knowledge, BestChunkPerDocument(
                nearest.Select(x => new RankedItem(x.DocumentId, x.Id, null, CosineSignal(x.Distance))), take));
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
                .Select(r => new RequestHitSource(
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
                .Select(c => new KnowledgeHitSource(c.DocumentId, c.Document.Title, c.Document.Kind, c.Document.CreatedAt, c.Text))
                .ToDictionaryAsync(c => c.DocumentId, ct);

        var hits = new List<SearchHit>(ranked.Count);
        foreach (var item in ranked)
        {
            if (item.Kind == SearchHitKind.Request && requests.TryGetValue(item.Id, out var request))
            {
                hits.Add(request.ToHit(item.Score, SearchText.Snippet(SnippetSource(request), plan.Stems)));
            }
            else if (item.Kind == SearchHitKind.Knowledge && knowledge.TryGetValue(item.Id, out var document))
            {
                hits.Add(document.ToHit(item.Score, SearchText.Snippet(document.Text, plan.Stems), LocalDate(document.CreatedAt)));
            }
        }

        return hits.OrderByDescending(h => h.Score).ThenByDescending(h => h.Date).ToArray();
    }

    private static DateOnly LocalDate(DateTimeOffset value) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(value, Warsaw).DateTime);

    private static string SnippetSource(RequestHitSource row)
    {
        if (row.Status == RequestStatus.ResponseSent && !string.IsNullOrWhiteSpace(row.DraftContent))
        {
            var body = row.DraftContent.IndexOf("w odpowiedzi na", StringComparison.OrdinalIgnoreCase);
            return body > 0 ? row.DraftContent[body..] : row.DraftContent;
        }

        return string.IsNullOrWhiteSpace(row.Content) ? row.Subject : row.Content;
    }

    private static List<RankedItem> BestChunkPerDocument(IEnumerable<RankedItem> chunks, int take) =>
        chunks
            .OrderByDescending(c => c.Signal)
            .DistinctBy(c => c.Id)
            .Take(take)
            .ToList();

    private sealed record QueryPlan(
        string Text, bool IsShort, IReadOnlyList<(string Token, int Count)> Tokens, IReadOnlyList<string> Stems)
    {
        public static QueryPlan From(string text)
        {
            var isShort = SearchText.IsShortQuery(text);
            var tokens = SearchText.QueryTokens(text, isShort ? 3 : 4, includeNumbers: isShort);
            return new QueryPlan(text, isShort, tokens, SearchText.Stems(tokens.Select(t => t.Token)));
        }
    }
}
