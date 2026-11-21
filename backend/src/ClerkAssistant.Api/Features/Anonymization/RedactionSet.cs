using System.Text;
using ClerkAssistant.Api.Domain;

namespace ClerkAssistant.Api.Features.Anonymization;

public static class RedactionSet
{
    public const string Placeholder = "[dane zanonimizowane]";

    public static List<Redaction> Merge(IEnumerable<Redaction> candidates)
    {
        var ordered = candidates
            .Where(r => r.Length > 0)
            .OrderBy(r => r.Start)
            .ThenByDescending(r => r.Length)
            .ThenBy(r => Priority(r.Source));
        var merged = new List<Redaction>();
        foreach (var candidate in ordered)
        {
            if (merged.Count == 0 || !Overlaps(merged[^1], candidate))
            {
                merged.Add(candidate);
                continue;
            }

            var kept = merged[^1];
            if (candidate.Length > kept.Length
                || (candidate.Length == kept.Length && Priority(candidate.Source) < Priority(kept.Source)))
            {
                merged[^1] = candidate;
            }
        }

        return merged;
    }

    public static List<Redaction> Relocate(string content, IEnumerable<Redaction> existing)
    {
        var placed = new List<Redaction>();
        foreach (var redaction in existing.Where(r => !string.IsNullOrEmpty(r.Text)).OrderBy(r => r.Start))
        {
            var start = NearestFreeOccurrence(content, redaction, placed);
            if (start >= 0)
            {
                placed.Add(redaction with { Start = start, Length = redaction.Text.Length });
            }
        }

        return placed.OrderBy(r => r.Start).ToList();
    }

    public static List<Redaction> AddSuggestions(IEnumerable<Redaction> placed, IEnumerable<Redaction> suggestions)
    {
        var result = placed.ToList();
        foreach (var suggestion in suggestions)
        {
            if (!result.Any(r => Overlaps(r, suggestion)))
            {
                result.Add(suggestion);
            }
        }

        return result.OrderBy(r => r.Start).ToList();
    }

    public static List<Redaction> Sanitize(string content, IEnumerable<Redaction?> redactions)
    {
        var valid = redactions
            .OfType<Redaction>()
            .Where(r => IsValid(content, r))
            .OrderBy(r => r.Start)
            .ThenByDescending(r => r.Length);
        var result = new List<Redaction>();
        foreach (var redaction in valid)
        {
            if (result.Count == 0 || End(result[^1]) <= redaction.Start)
            {
                result.Add(redaction);
            }
        }

        return result;
    }

    public static bool IsValid(string content, Redaction redaction) =>
        redaction.Start >= 0
        && redaction.Length > 0
        && (long)redaction.Start + redaction.Length <= content.Length
        && redaction.Text is not null
        && redaction.Text.Length == redaction.Length
        && string.CompareOrdinal(content, redaction.Start, redaction.Text, 0, redaction.Length) == 0
        && Enum.IsDefined(redaction.Category)
        && Enum.IsDefined(redaction.Status)
        && Enum.IsDefined(redaction.Source);

    public static (string Text, int Replaced) ApplyApproved(string content, IEnumerable<Redaction> redactions)
    {
        var approved = Sanitize(content, redactions.Where(r => r.Status == RedactionStatus.Approved));
        var builder = new StringBuilder(content);
        foreach (var redaction in approved.OrderByDescending(r => r.Start))
        {
            builder.Remove(redaction.Start, redaction.Length).Insert(redaction.Start, Placeholder);
        }

        return (builder.ToString(), approved.Count);
    }

    public static bool Overlaps(Redaction a, Redaction b) => a.Start < End(b) && b.Start < End(a);

    private static int End(Redaction r) => r.Start + r.Length;

    private static int NearestFreeOccurrence(string content, Redaction redaction, List<Redaction> placed)
    {
        var best = -1;
        var index = content.IndexOf(redaction.Text, StringComparison.Ordinal);
        while (index >= 0)
        {
            var candidate = redaction with { Start = index, Length = redaction.Text.Length };
            if (!placed.Any(p => Overlaps(p, candidate))
                && (best < 0 || Math.Abs(index - redaction.Start) < Math.Abs(best - redaction.Start)))
            {
                best = index;
            }

            index = content.IndexOf(redaction.Text, index + 1, StringComparison.Ordinal);
        }

        return best;
    }

    private static int Priority(RedactionSource source) => source switch
    {
        RedactionSource.Manual => 0,
        RedactionSource.Rule => 1,
        _ => 2,
    };
}
