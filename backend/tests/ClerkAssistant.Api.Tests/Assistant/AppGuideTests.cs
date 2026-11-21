using ClerkAssistant.Api.Features.Assistant;

namespace ClerkAssistant.Api.Tests.Assistant;

public class AppGuideTests
{
    private const string Sample = """
        # Przewodnik testowy

        Wstęp, który nie należy do żadnej sekcji.

        ## Rejestr wniosków
        Adres: /

        Lista wniosków z terminami odpowiedzi.
        - Filtr roku pokazuje sprawy z wybranego roku.

        ## Sprawa
        Adres: `/wnioski/:id`, /wnioski/:id/odpowiedz (projekt)

        Szczegóły sprawy, wydziały i dokumenty.

        ## Rejestracja wniosku
        Adres: /wnioski/nowy

        Formularz rejestracji nowego wniosku z pliku lub tekstu.

        ## Przedłużenie terminu

        Termin można przedłużyć najwyżej do dwóch miesięcy od dnia wpływu wniosku.
        Przedłużenie wymaga podania powodu opóźnienia.

        ## Anonimizacja
        Adres: /anonimizacja

        Narzędzie do anonimizacji dowolnego dokumentu.
        """;

    private static readonly AppGuide Guide = new(Sample);

    [Fact]
    public void Parser_reads_sections_routes_and_bodies()
    {
        var sections = AppGuide.Parse(Sample);

        Assert.Equal(["Rejestr wniosków", "Sprawa", "Rejestracja wniosku", "Przedłużenie terminu", "Anonimizacja"],
            sections.Select(s => s.Title));
        Assert.Equal(["/"], sections[0].Routes);
        Assert.Equal("Lista wniosków z terminami odpowiedzi.\n- Filtr roku pokazuje sprawy z wybranego roku.", sections[0].Body);
        Assert.Equal(["/wnioski/:id", "/wnioski/:id/odpowiedz"], sections[1].Routes);
        Assert.Null(sections[1].Link);
        Assert.Equal("/wnioski/nowy", sections[2].Link);
        Assert.Empty(sections[3].Routes);
        Assert.StartsWith("Termin można przedłużyć", sections[3].Body);
        Assert.DoesNotContain(sections, s => s.Body.Contains("Wstęp"));
    }

    [Fact]
    public void Parser_treats_a_late_address_line_as_body_text()
    {
        var sections = AppGuide.Parse("## Temat\nPierwsze zdanie.\nAdres: /archiwum\n");

        var section = Assert.Single(sections);
        Assert.Empty(section.Routes);
        Assert.Contains("Adres: /archiwum", section.Body);
    }

    [Theory]
    [InlineData("/", "Rejestr wniosków")]
    [InlineData("/?rok=2026", "Rejestr wniosków")]
    [InlineData("/wnioski/0193a2b4-7c1d-7e2f-8a3b-4c5d6e7f8a9b", "Sprawa")]
    [InlineData("/wnioski/0193a2b4-7c1d-7e2f-8a3b-4c5d6e7f8a9b/odpowiedz", "Sprawa")]
    [InlineData("/wnioski/nowy", "Rejestracja wniosku")]
    [InlineData("/anonimizacja/", "Anonimizacja")]
    public void Page_is_found_by_route_pattern(string path, string title)
    {
        Assert.Equal(title, Guide.PageFor(path)?.Title);
    }

    [Theory]
    [InlineData("/wnioski/abc")]
    [InlineData("/nieznana-strona")]
    [InlineData("/wnioski")]
    public void Unknown_pages_have_no_section(string path)
    {
        Assert.Null(Guide.PageFor(path));
    }

    [Fact]
    public void Ranking_prefers_sections_whose_words_match_the_question()
    {
        var ranked = Guide.Rank("Jak przedłużyć termin odpowiedzi?", 3);

        Assert.Equal("Przedłużenie terminu", ranked[0].Title);
        Assert.Contains(ranked, s => s.Title == "Rejestr wniosków");
    }

    [Fact]
    public void Ranking_returns_nothing_for_unrelated_questions()
    {
        Assert.Empty(Guide.Rank("pogoda w Toruniu", 3));
        Assert.Empty(Guide.Rank("?!", 3));
    }

    [Fact]
    public void Embedded_guide_is_available()
    {
        var guide = AppGuide.FromEmbeddedResource();

        Assert.NotEmpty(guide.Text);
        Assert.NotEmpty(guide.Sections);
    }
}
