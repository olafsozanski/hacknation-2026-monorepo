using System.Text;
using ClerkAssistant.Api.Features.Search;

namespace ClerkAssistant.Api.Features.Assistant;

public static class AssistantFallback
{
    public const int GuideSections = 3;

    private const int MaxExcerpt = 240;
    private static readonly char[] MarkdownMarks = ['*', '`', '[', ']'];

    public static string Answer(AssistantSituation situation, AppGuide guide, string question)
    {
        var parts = new List<string>
        {
            "Odpowiadam w trybie podstawowym — bez modelu AI, na podstawie przewodnika i danych z aplikacji.",
        };
        AddScreenProblems(parts, situation.Screen);
        if (situation.Case is { } sheet)
        {
            parts.Add(CaseSummary(sheet));
        }

        var sections = GuideFor(situation, guide, question);
        if (sections.Count > 0)
        {
            parts.Add("Może pomóc:\n" + string.Join("\n", sections.Select(SectionLine)));
        }
        else if (parts.Count == 1)
        {
            parts.Add("Nie znalazłem w przewodniku informacji pasujących do pytania. Spróbuj zapytać innymi słowami " +
                      "albo wróć do [rejestru wniosków](/).");
        }

        return string.Join("\n\n", parts);
    }

    private static void AddScreenProblems(List<string> parts, ScreenContext screen)
    {
        var errors = (screen.FieldErrors ?? [])
            .Where(e => e is not null && !string.IsNullOrWhiteSpace(e.Message))
            .Select(e => string.IsNullOrWhiteSpace(e.Label)
                ? $"- {Plain(e.Message!)}"
                : $"- **{Plain(e.Label)}**: {Plain(e.Message!)}")
            .ToList();
        if (errors.Count > 0)
        {
            parts.Add("Na ekranie są błędy w formularzu — popraw te pola:\n" + string.Join("\n", errors));
        }

        var alerts = (screen.Alerts ?? []).Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => $"- {Plain(a)}").ToList();
        if (alerts.Count > 0)
        {
            parts.Add("Komunikaty widoczne na ekranie:\n" + string.Join("\n", alerts));
        }
    }

    private static string CaseSummary(CaseFactSheet sheet)
    {
        var text = new StringBuilder($"Sprawa **{sheet.CaseNumber}** ([otwórz sprawę]({sheet.Link})):");
        foreach (var fact in sheet.Facts)
        {
            text.Append("\n- ").Append(Plain(fact));
        }

        text.Append(sheet.Blockers.Count == 0 ? "\n\nNic nie blokuje kolejnego kroku." : "\n\nCo blokuje kolejny krok:");
        foreach (var blocker in sheet.Blockers)
        {
            text.Append("\n- ").Append(blocker);
        }

        return text.Append("\n\n**Zalecany kolejny krok:** ").Append(sheet.NextStep).ToString();
    }

    private static List<GuideSection> GuideFor(AssistantSituation situation, AppGuide guide, string question)
    {
        var ranked = guide.Rank(question, GuideSections);
        return situation.Page is { } page ? ranked.Append(page).Distinct().Take(GuideSections).ToList() : ranked.ToList();
    }

    private static string SectionLine(GuideSection section)
    {
        var title = section.Link is { } link ? $"[{section.Title}]({link})" : $"**{section.Title}**";
        var excerpt = Excerpt(section.Body);
        return excerpt.Length == 0 ? $"- {title}" : $"- {title} — {excerpt}";
    }

    private static string Excerpt(string body)
    {
        var first = body.Split('\n').Select(l => l.TrimStart(' ', '-').Trim()).FirstOrDefault(l => l.Length > 0) ?? "";
        return AssistantWording.Clip(first, MaxExcerpt);
    }

    private static string Plain(string text) =>
        SearchText.CollapseWhitespace(string.Concat(text.Where(c => !MarkdownMarks.Contains(c))));
}
