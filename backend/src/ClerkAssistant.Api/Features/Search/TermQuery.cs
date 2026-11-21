using NpgsqlTypes;

namespace ClerkAssistant.Api.Features.Search;

internal sealed record WeightedTerm(string Stem, bool Prefix, double Weight)
{
    public string TsQueryTerm => Prefix ? $"{Stem}:*" : Stem;

    public bool Matches(string lexeme) =>
        Prefix ? lexeme.StartsWith(Stem, StringComparison.Ordinal) : lexeme == Stem;
}

internal sealed class TermQuery
{
    public const int MaxTerms = 40;
    public const int MinDocumentsForCommonTermFilter = 30;
    public const double MaxTermShare = 0.3;

    private TermQuery(IReadOnlyList<WeightedTerm> terms)
    {
        Terms = terms;
        TotalWeight = terms.Sum(t => t.Weight);
    }

    public IReadOnlyList<WeightedTerm> Terms { get; }

    public double TotalWeight { get; }

    public bool IsEmpty => Terms.Count == 0 || TotalWeight <= 0;

    public string TsQuery => string.Join(" | ", Terms.Select(t => t.TsQueryTerm));

    public static TermQuery Build(
        IReadOnlyList<(string Token, int Count)> tokens, CorpusSnapshot corpus, bool dropCommonTerms)
    {
        var filterCommon = dropCommonTerms && corpus.Documents >= MinDocumentsForCommonTermFilter;
        var terms = tokens
            .Where(t => SearchText.IsSafeToken(t.Token))
            .GroupBy(t => (Stem: SearchText.Stem(t.Token), Prefix: SearchText.IsStemmable(t.Token)))
            .Select(g => (g.Key.Stem, g.Key.Prefix, Count: g.Sum(t => t.Count)))
            .Where(t => !filterCommon || corpus.Share(t.Stem, t.Prefix) <= MaxTermShare)
            .Select(t => new WeightedTerm(
                t.Stem, t.Prefix, corpus.InverseDocumentFrequency(t.Stem, t.Prefix) * (1 + 0.5 * Math.Log(t.Count))))
            .OrderByDescending(t => t.Weight)
            .ThenBy(t => t.Stem, StringComparer.Ordinal)
            .Take(MaxTerms)
            .ToList();
        return new TermQuery(terms);
    }

    public double Coverage(NpgsqlTsVector vector)
    {
        if (IsEmpty)
        {
            return 0;
        }

        var lexemes = vector.Select(l => l.Text).ToArray();
        return Terms.Where(t => lexemes.Any(t.Matches)).Sum(t => t.Weight) / TotalWeight;
    }
}
