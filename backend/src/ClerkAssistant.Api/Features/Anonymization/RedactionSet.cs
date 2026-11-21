using System.Text;
using ClerkAssistant.Api.Domain;

namespace ClerkAssistant.Api.Features.Anonymization;

public static class RedactionSet
{
    public const string Placeholder = "[dane zanonimizowane]";
    public const int MaxRedactions = 5000;
    private const int MaxOccurrenceProbes = 64;

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

    public static List<Redaction> Relocate(string content, IEnumerable<Redaction> existing, CancellationToken ct = default)
    {
        var placed = new IntervalSet();
        foreach (var redaction in existing.Where(r => !string.IsNullOrEmpty(r.Text)).OrderBy(r => r.Start).Take(MaxRedactions))
        {
            ct.ThrowIfCancellationRequested();
            var start = NearestFreeOccurrence(content, redaction, placed);
            if (start >= 0)
            {
                placed.Add(redaction with { Start = start, Length = redaction.Text.Length });
            }
        }

        return placed.ToList();
    }

    public static List<Redaction> AddSuggestions(
        IEnumerable<Redaction> placed, IEnumerable<Redaction> suggestions, CancellationToken ct = default)
    {
        var result = new IntervalSet();
        foreach (var redaction in placed)
        {
            result.TryAdd(redaction);
        }

        foreach (var suggestion in suggestions)
        {
            ct.ThrowIfCancellationRequested();
            result.TryAdd(suggestion);
        }

        return result.ToList();
    }

    public static List<Redaction> Sanitize(string content, IEnumerable<Redaction?> redactions)
    {
        var valid = redactions
            .OfType<Redaction>()
            .Take(MaxRedactions)
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
        var builder = new StringBuilder(content.Length);
        var position = 0;
        foreach (var redaction in approved)
        {
            builder.Append(content, position, redaction.Start - position).Append(Placeholder);
            position = End(redaction);
        }

        builder.Append(content, position, content.Length - position);
        return (builder.ToString(), approved.Count);
    }

    public static bool Overlaps(Redaction a, Redaction b) => a.Start < End(b) && b.Start < End(a);

    private static int End(Redaction r) => r.Start + r.Length;

    private static int NearestFreeOccurrence(string content, Redaction redaction, IntervalSet placed)
    {
        var anchor = Math.Clamp(redaction.Start, 0, content.Length);
        var after = FirstFree(content, redaction.Text, placed, anchor, forward: true);
        var before = FirstFree(content, redaction.Text, placed, anchor - 1, forward: false);
        if (after < 0 || before < 0)
        {
            return Math.Max(after, before);
        }

        return after - anchor <= anchor - before ? after : before;
    }

    private static int FirstFree(string content, string text, IntervalSet placed, int from, bool forward)
    {
        var index = forward ? NextOccurrence(content, text, from) : PreviousOccurrence(content, text, from);
        for (var probe = 0; index >= 0 && probe < MaxOccurrenceProbes; probe++)
        {
            if (!placed.Overlaps(index, text.Length))
            {
                return index;
            }

            index = forward ? NextOccurrence(content, text, index + 1) : PreviousOccurrence(content, text, index - 1);
        }

        return -1;
    }

    private static int NextOccurrence(string content, string text, int from) =>
        from < 0 || from > content.Length - text.Length ? -1 : content.IndexOf(text, from, StringComparison.Ordinal);

    private static int PreviousOccurrence(string content, string text, int startAtOrBefore)
    {
        if (startAtOrBefore < 0 || content.Length < text.Length)
        {
            return -1;
        }

        var lastStart = Math.Min(startAtOrBefore, content.Length - text.Length);
        return content.LastIndexOf(text, lastStart + text.Length - 1, StringComparison.Ordinal);
    }

    private static int Priority(RedactionSource source) => source switch
    {
        RedactionSource.Manual => 0,
        RedactionSource.Rule => 1,
        _ => 2,
    };

    private sealed class IntervalSet
    {
        private readonly SortedSet<Redaction> _items = new(Comparer<Redaction>.Create((a, b) =>
            a.Start != b.Start ? a.Start.CompareTo(b.Start) : a.Length.CompareTo(b.Length)));

        public bool Overlaps(int start, int length)
        {
            if (_items.Count == 0)
            {
                return false;
            }

            var before = _items.Min!.Start <= start ? _items.GetViewBetween(_items.Min, Probe(start, int.MaxValue)).Max : null;
            if (before is not null && before.Start + before.Length > start)
            {
                return true;
            }

            var after = _items.Max!.Start > start ? _items.GetViewBetween(Probe(start + 1, 0), _items.Max).Min : null;
            return after is not null && after.Start < start + length;
        }

        private static Redaction Probe(int start, int length) =>
            new(start, length, "", RedactionCategory.Other, RedactionStatus.Suggested, RedactionSource.Rule);

        public void Add(Redaction redaction) => _items.Add(redaction);

        public bool TryAdd(Redaction redaction)
        {
            if (redaction.Length <= 0 || Overlaps(redaction.Start, redaction.Length))
            {
                return false;
            }

            _items.Add(redaction);
            return true;
        }

        public List<Redaction> ToList() => _items.ToList();
    }
}
