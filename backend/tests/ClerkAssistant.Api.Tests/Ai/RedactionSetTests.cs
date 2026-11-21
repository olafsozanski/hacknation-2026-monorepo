using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Features.Anonymization;
using ClerkAssistant.Api.Features.Drafts;

namespace ClerkAssistant.Api.Tests.Ai;

public class RedactionSetTests
{
    [Fact]
    public void Merge_keeps_the_longer_span_and_prefers_rules_on_ties()
    {
        var merged = RedactionSet.Merge(
        [
            Redaction(10, 5, RedactionSource.Ai),
            Redaction(0, 4, RedactionSource.Ai),
            Redaction(0, 4, RedactionSource.Rule),
            Redaction(8, 12, RedactionSource.Ai),
            Redaction(25, 3, RedactionSource.Rule),
            Redaction(26, 1, RedactionSource.Rule),
        ]);

        Assert.Equal(
            [(0, 4, RedactionSource.Rule), (8, 12, RedactionSource.Ai), (25, 3, RedactionSource.Rule)],
            merged.Select(r => (r.Start, r.Length, r.Source)));
    }

    [Fact]
    public void Relocate_follows_text_after_edits_and_drops_removed_fragments()
    {
        const string before = "Jan Kowalski prosi o informację. Kontakt: jan@example.com. Jan Kowalski.";
        var existing = new[]
        {
            new Redaction(0, 12, "Jan Kowalski", RedactionCategory.PersonName, RedactionStatus.Approved, RedactionSource.Manual),
            new Redaction(42, 15, "jan@example.com", RedactionCategory.Email, RedactionStatus.Rejected, RedactionSource.Rule),
            new Redaction(59, 12, "Jan Kowalski", RedactionCategory.PersonName, RedactionStatus.Suggested, RedactionSource.Ai),
        };
        Assert.All(existing, r => Assert.Equal(r.Text, before.Substring(r.Start, r.Length)));
        const string after = "Szanowni Państwo, Jan Kowalski prosi o informację. Jan Kowalski.";

        var relocated = RedactionSet.Relocate(after, existing);

        Assert.Equal(2, relocated.Count);
        Assert.Equal((18, RedactionStatus.Approved, RedactionSource.Manual), (relocated[0].Start, relocated[0].Status, relocated[0].Source));
        Assert.Equal((51, RedactionStatus.Suggested, RedactionSource.Ai), (relocated[1].Start, relocated[1].Status, relocated[1].Source));
        Assert.All(relocated, r => Assert.Equal(r.Text, after.Substring(r.Start, r.Length)));
    }

    [Fact]
    public void New_suggestions_do_not_override_existing_decisions()
    {
        var placed = new List<Redaction>
        {
            new(5, 10, "0123456789", RedactionCategory.Other, RedactionStatus.Rejected, RedactionSource.Rule),
        };

        var result = RedactionSet.AddSuggestions(placed,
        [
            Redaction(7, 3, RedactionSource.Rule),
            Redaction(20, 3, RedactionSource.Ai),
        ]);

        Assert.Equal([(5, RedactionStatus.Rejected), (20, RedactionStatus.Suggested)], result.Select(r => (r.Start, r.Status)));
    }

    [Fact]
    public void Sanitize_drops_invalid_and_overlapping_redactions()
    {
        const string content = "Jan Kowalski, tel. 501 234 567";

        var result = RedactionSet.Sanitize(content,
        [
            new Redaction(0, 12, "Jan Kowalski", RedactionCategory.PersonName, RedactionStatus.Approved, RedactionSource.Manual),
            new Redaction(4, 8, "Kowalski", RedactionCategory.PersonName, RedactionStatus.Approved, RedactionSource.Manual),
            new Redaction(19, 11, "999 999 999", RedactionCategory.Phone, RedactionStatus.Approved, RedactionSource.Manual),
            new Redaction(25, 50, "567", RedactionCategory.Phone, RedactionStatus.Approved, RedactionSource.Manual),
            new Redaction(-1, 3, "Jan", RedactionCategory.PersonName, RedactionStatus.Approved, RedactionSource.Manual),
            null,
        ]);

        Assert.Equal(["Jan Kowalski"], result.Select(r => r.Text));
    }

    [Fact]
    public void Export_replaces_only_approved_redactions_and_adds_notice()
    {
        const string content = "Jan Kowalski\njan@example.com\n\nTreść odpowiedzi dla Jan Kowalski.";
        var redactions = new[]
        {
            new Redaction(0, 12, "Jan Kowalski", RedactionCategory.PersonName, RedactionStatus.Approved, RedactionSource.Rule),
            new Redaction(13, 15, "jan@example.com", RedactionCategory.Email, RedactionStatus.Suggested, RedactionSource.Rule),
            new Redaction(51, 12, "Jan Kowalski", RedactionCategory.PersonName, RedactionStatus.Rejected, RedactionSource.Rule),
        };

        var text = DraftExport.PrepareText(content, redactions);

        Assert.Equal(
            "[dane zanonimizowane]\njan@example.com\n\nTreść odpowiedzi dla Jan Kowalski.\n\n" + DraftExport.AnonymizationNotice,
            text);
    }

    [Fact]
    public void Export_without_approved_redactions_has_no_notice() =>
        Assert.Equal("Treść.", DraftExport.PrepareText("Treść.", []));

    [Theory]
    [InlineData("BKS.1431.12.2026", "docx", "BKS-1431-12-2026-odpowiedz.docx")]
    [InlineData("BKS/1431 Ł.7", "txt", "BKS-1431---7-odpowiedz.txt")]
    public void Export_file_name_is_derived_from_case_number(string caseNumber, string extension, string expected) =>
        Assert.Equal(expected, DraftExport.FileName(caseNumber, extension));

    private static Redaction Redaction(int start, int length, RedactionSource source) =>
        new(start, length, new string('x', length), RedactionCategory.Other, RedactionStatus.Suggested, source);
}
