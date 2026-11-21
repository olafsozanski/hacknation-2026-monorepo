using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Features.Drafts;
using ClerkAssistant.Api.Features.Intake;
using ClerkAssistant.Api.Features.Knowledge;
using ClerkAssistant.Api.Features.Search;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace ClerkAssistant.Api.Tests.Ai;

public class TextProcessingTests
{
    [Fact]
    public void Tsquery_contains_only_safe_terms_whatever_the_input()
    {
        const string hostile = "'); DROP TABLE requests; -- | & ! <-> :* ( ) \\ \"koszty\" sylwestra:* 2025";

        var query = SearchText.BuildOrQuery(SearchText.DistinctiveTokens(hostile));

        Assert.NotNull(query);
        Assert.Matches(@"^[\p{Ll}\d]+(:\*)?( \| [\p{Ll}\d]+(:\*)?)*$", query);
        Assert.DoesNotContain("'", query);
        Assert.DoesNotContain(";", query);
        Assert.Contains("sylwes:*", query);
        Assert.Contains("2025", query);
    }

    [Fact]
    public void Distinctive_tokens_drop_stopwords_and_short_words()
    {
        var tokens = SearchText.DistinctiveTokens(
            "Wnoszę o udostępnienie informacji publicznej, które dotyczą kosztów remontu ulicy oraz remontu chodnika.");

        Assert.Equal("remontu", tokens[0]);
        Assert.Contains("kosztów", tokens);
        Assert.Contains("chodnika", tokens);
        Assert.DoesNotContain("informacji", tokens);
        Assert.DoesNotContain("publicznej", tokens);
        Assert.DoesNotContain("oraz", tokens);
        Assert.DoesNotContain("o", tokens);
    }

    [Theory]
    [InlineData("kosztów", "koszt")]
    [InlineData("sylwestra", "sylwes")]
    [InlineData("organizacji", "organiza")]
    [InlineData("budowy", "budow")]
    [InlineData("drogi", "drogi")]
    [InlineData("2025", "2025")]
    [InlineData("1234567", "1234567")]
    public void Stemming_softens_polish_inflection(string token, string expected) =>
        Assert.Equal(expected, SearchText.Stem(token));

    [Fact]
    public void Too_many_tokens_are_capped()
    {
        var text = string.Join(' ', Enumerable.Range(0, 60).Select(i => $"słowo{i:D2}"));

        Assert.Equal(SearchText.MaxQueryTokens, SearchText.DistinctiveTokens(text).Count);
    }

    [Fact]
    public void Snippet_is_plain_and_centred_on_a_match()
    {
        var text = new string('a', 10) + " " + string.Join(' ', Enumerable.Repeat("wstęp", 80)) +
                   " Koszt organizacji Sylwestra wyniósł 100 zł. " + string.Join(' ', Enumerable.Repeat("koniec", 80));

        var snippet = SearchText.Snippet(text, SearchText.Stems(["sylwestra"]));

        Assert.True(snippet.Length <= 242);
        Assert.StartsWith("…", snippet);
        Assert.Contains("Sylwestra", snippet);
        Assert.DoesNotContain("\n", snippet);
    }

    [Fact]
    public void Chunker_splits_into_overlapping_chunks_of_bounded_size()
    {
        var paragraphs = Enumerable.Range(1, 40)
            .Select(i => $"Paragraf {i}. " + string.Join(' ', Enumerable.Repeat($"treść{i}", 20)))
            .ToList();
        var text = string.Join("\n\n", paragraphs);

        var chunks = TextChunker.Split(text);

        Assert.True(chunks.Count > 3);
        Assert.All(chunks, c => Assert.InRange(c.Length, 1, TextChunker.TargetLength));
        foreach (var paragraph in paragraphs)
        {
            Assert.Contains(chunks, c => c.Contains(paragraph));
        }

        for (var i = 1; i < chunks.Count; i++)
        {
            var tail = chunks[i - 1][^50..];
            Assert.Contains(tail, chunks[i]);
        }
    }

    [Fact]
    public void Chunker_splits_a_single_huge_paragraph()
    {
        var text = string.Join(' ', Enumerable.Repeat("słowo", 2000));

        var chunks = TextChunker.Split(text);

        Assert.True(chunks.Count >= 9);
        Assert.All(chunks, c => Assert.True(c.Length <= TextChunker.TargetLength));
    }

    [Fact]
    public void Docx_export_is_a_valid_a4_document_in_polish()
    {
        const string text = "Bydgoszcz, dnia 21 listopada 2026 r.\n\nZnak sprawy: BKS.IV.1431.1.2026\n\nJan Kowalski\nul. Długa 5\n\nTreść & <znaczniki> \u0001 odpowiedzi.";

        var bytes = DraftExport.ToDocx(text, "Odpowiedź");

        using var stream = new MemoryStream(bytes);
        using var document = WordprocessingDocument.Open(stream, false);
        var body = document.MainDocumentPart!.Document!.Body!;
        var paragraphs = body.Elements<Paragraph>().ToList();
        Assert.Equal(4, paragraphs.Count);
        Assert.Equal("Bydgoszcz, dnia 21 listopada 2026 r.", paragraphs[0].InnerText);
        Assert.Equal(JustificationValues.Right, paragraphs[0].ParagraphProperties?.Justification?.Val?.Value);
        Assert.Single(paragraphs[2].Descendants<Break>());
        Assert.Equal("Treść & <znaczniki>  odpowiedzi.", paragraphs[3].InnerText);

        var section = body.GetFirstChild<SectionProperties>()!;
        Assert.Equal(11906u, section.GetFirstChild<PageSize>()!.Width!.Value);
        Assert.Equal(1417, section.GetFirstChild<PageMargin>()!.Top!.Value);
        var defaults = document.MainDocumentPart.StyleDefinitionsPart!.Styles!.DocDefaults!;
        Assert.Equal("pl-PL", defaults.Descendants<Languages>().Single().Val!.Value);
        Assert.Equal("Arial", defaults.Descendants<RunFonts>().Single().Ascii!.Value);
    }

    [Fact]
    public void Model_body_is_stripped_of_markdown_header_and_closing()
    {
        const string model = "Znak sprawy: X\n\nW odpowiedzi na wniosek **uprzejmie** informuję, że:\n\n## Punkt 1\n* koszt wyniósł 100 zł\n\n\n\nZ poważaniem\nJan";

        var body = DraftComposer.CleanModelBody(model);

        Assert.Equal("W odpowiedzi na wniosek uprzejmie informuję, że:\n\nPunkt 1\n- koszt wyniósł 100 zł", body);
    }

    [Fact]
    public void Intake_heuristics_extract_basic_fields()
    {
        const string letter = """
            Jan Kowalski
            ul. Długa 5/3
            85-001 Bydgoszcz
            Bydgoszcz, 15.11.2026 r.

            Urząd Miasta Bydgoszczy
            ul. Jezuicka 1, 85-102 Bydgoszcz

            Wniosek o udostępnienie informacji publicznej

            Na podstawie art. 2 ust. 1 ustawy z dnia 6 września 2001 r. o dostępie do informacji publicznej wnoszę o udostępnienie informacji dotyczących kosztów organizacji Sylwestra Miejskiego 2025. W szczególności proszę o:
            1. łączny koszt wydarzenia,
            2. koszt wynagrodzeń artystów.

            Odpowiedź proszę przesłać na adres e-mail: jan.kowalski@example.com.

            Z poważaniem
            Jan Kowalski
            """;

        var suggestion = IntakeHeuristics.Analyze(
            letter.Replace("\r\n", "\n"), new DateOnly(2026, 11, 18), ["um.bydgoszcz.pl"], "ul. Jezuicka 1, 85-102 Bydgoszcz");

        Assert.Equal("Kosztów organizacji Sylwestra Miejskiego 2025", suggestion.Subject);
        Assert.Equal("Jan Kowalski", suggestion.RequesterName);
        Assert.Equal("jan.kowalski@example.com", suggestion.RequesterContact);
        Assert.Equal(ReplyChannel.Email, suggestion.ReplyChannel);
        Assert.Equal(new DateOnly(2026, 11, 15), suggestion.RequestDate);
        Assert.Equal(["łączny koszt wydarzenia", "koszt wynagrodzeń artystów."], suggestion.RequestedItems);
        Assert.True(suggestion.LooksLikePublicInformationRequest);
    }

    [Fact]
    public void Intake_heuristics_flag_petitions_and_postal_contact()
    {
        const string letter = "Petycja w sprawie budowy ścieżki rowerowej na ul. Fordońskiej.\nAnna Nowak\nul. Polna 3\n86-300 Grudziądz";

        var suggestion = IntakeHeuristics.Analyze(letter, new DateOnly(2026, 11, 18), [], null);

        Assert.False(suggestion.LooksLikePublicInformationRequest);
        Assert.Equal("ul. Polna 3, 86-300 Grudziądz", suggestion.RequesterContact);
        Assert.Contains("petycją", suggestion.Remarks);
        Assert.StartsWith("Petycja w sprawie budowy", suggestion.Subject);
    }
}
