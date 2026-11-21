using System.Text;
using System.Text.RegularExpressions;

namespace ClerkAssistant.Api.Features.Knowledge;

public static partial class TextChunker
{
    public const int TargetLength = 1200;
    public const int OverlapLength = 200;

    public static List<string> Split(string text, int targetLength = TargetLength, int overlapLength = OverlapLength)
    {
        var chunks = new List<string>();
        var current = new StringBuilder();
        foreach (var unit in Units(text, targetLength))
        {
            if (current.Length > 0 && current.Length + 1 + unit.Length > targetLength)
            {
                var chunk = current.ToString();
                chunks.Add(chunk);
                current.Clear();
                var tail = Tail(chunk, overlapLength);
                if (tail.Length > 0 && tail.Length + 1 + unit.Length <= targetLength)
                {
                    current.Append(tail);
                }
            }

            if (current.Length > 0)
            {
                current.Append('\n');
            }

            current.Append(unit);
        }

        if (current.Length > 0)
        {
            chunks.Add(current.ToString());
        }

        return chunks;
    }

    private static IEnumerable<string> Units(string text, int maxLength)
    {
        foreach (var paragraph in ParagraphBreak().Split(text.Replace("\r\n", "\n")))
        {
            var trimmed = paragraph.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            var rest = trimmed;
            while (rest.Length > maxLength)
            {
                var cut = BreakPoint(rest, maxLength);
                yield return rest[..cut].Trim();
                rest = rest[cut..].TrimStart();
            }

            if (rest.Length > 0)
            {
                yield return rest;
            }
        }
    }

    private static int BreakPoint(string text, int maxLength)
    {
        var window = text[..maxLength];
        var line = window.LastIndexOf('\n');
        if (line > maxLength / 2)
        {
            return line + 1;
        }

        var sentence = window.LastIndexOfAny(['.', '?', '!', ';']);
        if (sentence > maxLength / 2)
        {
            return sentence + 1;
        }

        var space = window.LastIndexOf(' ');
        return space > maxLength / 3 ? space + 1 : maxLength;
    }

    private static string Tail(string chunk, int length)
    {
        if (chunk.Length <= length)
        {
            return chunk;
        }

        var start = chunk.Length - length;
        var boundary = chunk.IndexOfAny([' ', '\n'], start);
        return (boundary >= 0 ? chunk[(boundary + 1)..] : chunk[start..]).Trim();
    }

    [GeneratedRegex(@"\n[ \t]*\n\s*")]
    private static partial Regex ParagraphBreak();
}
