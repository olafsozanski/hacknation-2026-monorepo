using System.Text.Json;
using ClerkAssistant.Api.Tests.Infrastructure;

namespace ClerkAssistant.Api.Tests.Assistant;

public sealed record SseEvent(string Type, JsonElement Data)
{
    public T As<T>() => Data.Deserialize<T>(ApiClient.Json)!;

    public string Text => Data.GetProperty("text").GetString()!;
}

public static class SseReader
{
    public static List<SseEvent> Parse(string stream)
    {
        var events = new List<SseEvent>();
        foreach (var block in stream.Replace("\r\n", "\n").Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
        {
            var type = "message";
            var data = new List<string>();
            foreach (var line in block.Split('\n'))
            {
                if (line.StartsWith("event:", StringComparison.Ordinal))
                {
                    type = line["event:".Length..].Trim();
                }
                else if (line.StartsWith("data:", StringComparison.Ordinal))
                {
                    var value = line["data:".Length..];
                    data.Add(value.StartsWith(' ') ? value[1..] : value);
                }
            }

            if (data.Count > 0)
            {
                using var document = JsonDocument.Parse(string.Join("\n", data));
                events.Add(new SseEvent(type, document.RootElement.Clone()));
            }
        }

        return events;
    }
}
