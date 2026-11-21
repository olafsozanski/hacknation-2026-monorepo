using System.Net;
using System.Net.Http.Json;
using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Features.Anonymization;
using ClerkAssistant.Api.Features.Drafts;
using ClerkAssistant.Api.Tests.Infrastructure;
using DocumentFormat.OpenXml.Packaging;
using Microsoft.EntityFrameworkCore;

namespace ClerkAssistant.Api.Tests.Ai;

public class AnonymizationToolTests(AiApiFactory factory) : IClassFixture<AiApiFactory>
{
    private const string Letter = """
        Bydgoszcz, 10.11.2026 r.

        Pan Jan Kowalski, PESEL 44051401359, zamieszkały ul. Długa 5, 85-001 Bydgoszcz,
        tel. 501 234 567, e-mail: jan.kowalski@gmail.com, jest właścicielem nieruchomości
        o księdze wieczystej BY1B/00012345/6 (działka nr 123/4) i pojazdu o nr rej. CB 12345.
        Jan Kowalski choruje przewlekle.

        Biuro Komunikacji Społecznej
        ul. Jezuicka 1, 85-102 Bydgoszcz
        tel. (52) 58 58 365, fax (52) 58 58 623
        e-mail: bks@um.bydgoszcz.pl, www.bydgoszcz.pl
        """;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Uploaded_file_is_analyzed_and_exported_with_personal_data_hidden()
    {
        var client = await factory.ClientAsync();
        var filesBefore = await factory.QueryAsync(db => db.Files.CountAsync(Ct));

        var analyzed = await client.PostAsync("/api/anonymization/analyze", TestData.TextFile("Wniosek Kowalski.txt", Letter), Ct);

        analyzed.EnsureSuccessStatusCode();
        var result = (await analyzed.Content.ReadFromJsonAsync<AnonymizationResult>(ApiClient.Json, Ct))!;
        Assert.Equal("Wniosek Kowalski.txt", result.FileName);
        Assert.False(result.AiUsed);
        Assert.Empty(result.Warnings);
        Assert.All(result.Redactions, r => Assert.Equal(r.Text, result.Text.Substring(r.Start, r.Length)));
        Assert.Equal(
            [
                ("Jan Kowalski", RedactionCategory.PersonName),
                ("44051401359", RedactionCategory.Pesel),
                ("ul. Długa 5, 85-001 Bydgoszcz", RedactionCategory.Address),
                ("501 234 567", RedactionCategory.Phone),
                ("jan.kowalski@gmail.com", RedactionCategory.Email),
                ("BY1B/00012345/6", RedactionCategory.PropertyRecord),
                ("123/4", RedactionCategory.PropertyRecord),
                ("CB 12345", RedactionCategory.VehicleRegistration),
                ("Jan Kowalski", RedactionCategory.PersonName),
            ],
            result.Redactions.Select(r => (r.Text, r.Category)));
        Assert.Equal(filesBefore, await factory.QueryAsync(db => db.Files.CountAsync(Ct)));

        var approved = result.Redactions.Select(r => r with { Status = RedactionStatus.Approved }).ToList();
        approved.Add(new Redaction(0, 5, "Błędny", RedactionCategory.Other, RedactionStatus.Approved, RedactionSource.Manual));
        var txt = await client.PostAsJsonAsync("/api/anonymization/export",
            new AnonymizationExportRequest(result.Text, approved.ToArray(), "txt", result.FileName), ApiClient.Json, Ct);

        txt.EnsureSuccessStatusCode();
        var text = await txt.Content.ReadAsStringAsync(Ct);
        Assert.Equal("Wniosek-Kowalski-zanonimizowany.txt", txt.Content.Headers.ContentDisposition!.FileNameStar);
        Assert.StartsWith("Bydgoszcz, 10.11.2026 r.", text);
        foreach (var secret in new[] { "Kowalski", "44051401359", "Długa", "501 234 567", "gmail", "BY1B", "123/4", "CB 12345" })
        {
            Assert.DoesNotContain(secret, text);
        }

        Assert.Contains("Pan [dane zanonimizowane], PESEL [dane zanonimizowane]", text);
        Assert.Contains("e-mail: bks@um.bydgoszcz.pl", text);
        Assert.Contains("tel. (52) 58 58 365", text);
        Assert.EndsWith(DraftExport.AnonymizationNotice, text);

        var docx = await client.PostAsJsonAsync("/api/anonymization/export",
            new AnonymizationExportRequest(result.Text, approved.ToArray(), null, null), ApiClient.Json, Ct);
        docx.EnsureSuccessStatusCode();
        Assert.Equal("dokument-zanonimizowany.docx", docx.Content.Headers.ContentDisposition!.FileNameStar);
        using (var document = WordprocessingDocument.Open(new MemoryStream(await docx.Content.ReadAsByteArrayAsync(Ct)), false))
        {
            var body = document.MainDocumentPart!.Document!.Body!.InnerText;
            Assert.Contains("[dane zanonimizowane]", body);
            Assert.DoesNotContain("44051401359", body);
        }

        var analyzedAudit = await factory.QueryAsync(db => db.AuditEntries
            .Where(a => a.Action == "anonymization.analyzed" && a.Details == "plik")
            .Select(a => new { a.EntityType, a.EntityId })
            .ToListAsync(Ct));
        Assert.Equal([("Document", (Guid?)null)], analyzedAudit.Select(a => (a.EntityType, a.EntityId)));

        var audit = await factory.QueryAsync(db => db.AuditEntries
            .Where(a => a.Action == "anonymization.exported")
            .OrderBy(a => a.Id)
            .Select(a => new { a.EntityType, a.EntityId, a.Details })
            .ToListAsync(Ct));
        Assert.Equal(
            [("Document", (Guid?)null, "txt"), ("Document", null, "docx")],
            audit.Select(a => (a.EntityType, a.EntityId, a.Details)));
    }

    [Fact]
    public async Task Pasted_text_is_normalised_and_offsets_match_the_returned_text()
    {
        var client = await factory.ClientAsync();

        var response = await client.PostAsJsonAsync("/api/anonymization/analyze-text",
            new AnalyzeTextRequest("Wnioskodawca: Anna Nowak\r\nur. 12.03.1980 r. w Toruniu\r\nnr rej. CT 9876A"), ApiClient.Json, Ct);

        response.EnsureSuccessStatusCode();
        var result = (await response.Content.ReadFromJsonAsync<AnonymizationResult>(ApiClient.Json, Ct))!;
        Assert.Null(result.FileName);
        Assert.DoesNotContain("\r", result.Text);
        Assert.True(await factory.QueryAsync(db => db.AuditEntries.AnyAsync(
            a => a.Action == "anonymization.analyzed" && a.Details == "tekst" && a.EntityType == "Document", Ct)));
        Assert.Equal(
            ["Anna Nowak", "12.03.1980 r. w Toruniu", "CT 9876A"],
            result.Redactions.Select(r => r.Text));
        Assert.All(result.Redactions, r => Assert.Equal(r.Text, result.Text.Substring(r.Start, r.Length)));
    }

    [Fact]
    public async Task Text_without_personal_data_returns_a_warning()
    {
        var client = await factory.ClientAsync();

        var response = await client.PostAsJsonAsync("/api/anonymization/analyze-text",
            new AnalyzeTextRequest("Budżet miasta na 2026 r. wynosi 3 mld zł."), ApiClient.Json, Ct);

        var result = (await response.Content.ReadFromJsonAsync<AnonymizationResult>(ApiClient.Json, Ct))!;
        Assert.Empty(result.Redactions);
        Assert.Equal([AnonymizationService.NothingFoundWarning], result.Warnings);
    }

    [Fact]
    public async Task Invalid_input_is_rejected_with_validation_problems()
    {
        var client = await factory.ClientAsync();

        var unsupported = await client.PostAsync("/api/anonymization/analyze", TestData.TextFile("plik.exe", "MZ"), Ct);
        var noFile = await client.PostAsync("/api/anonymization/analyze", new MultipartFormDataContent(), Ct);
        var badFormat = await client.PostAsJsonAsync("/api/anonymization/export",
            new AnonymizationExportRequest("Treść", [], "pdf", null), ApiClient.Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, unsupported.StatusCode);
        Assert.Contains("\"file\"", await unsupported.Content.ReadAsStringAsync(Ct));
        Assert.Equal(HttpStatusCode.BadRequest, noFile.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badFormat.StatusCode);
        Assert.Contains("\"format\"", await badFormat.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task Draft_for_data_held_by_another_entity_follows_the_redirect_letter()
    {
        var id = await factory.AddRequestAsync(new NewRequest(
            "Realizacja umowy nr 102/TT/2019", "Proszę o informację o realizacji umowy nr 102/TT/2019.", new DateOnly(2026, 11, 16))
        {
            RequesterName = "Anna Nowak",
        });
        await factory.AddDepartmentReplyAsync(id, "WIM",
            "Wydział nie jest dysponentem wnioskowanych informacji. Właściwym dysponentem danych jest Zarząd Dróg Miejskich " +
            "i Komunikacji Publicznej w Bydgoszczy, ul. Toruńska 174a, 85-844 Bydgoszcz, e-mail: zarzad@zdmikp.bydgoszcz.pl.");
        var client = await factory.ClientAsync();

        var response = await client.PostAsync($"/api/requests/{id}/draft/generate", null, Ct);

        response.EnsureSuccessStatusCode();
        var result = (await response.Content.ReadFromJsonAsync<GenerateDraftResult>(ApiClient.Json, Ct))!;
        Assert.Contains(DraftComposer.NotHolderWarning, result.Warnings);
        var content = result.Draft.Content;
        Assert.Contains(
            "Szanowna Pani,\n\nw odpowiedzi na wystosowany przez Panią wniosek o udostępnienie informacji publicznej dotyczący: " +
            "„Realizacja umowy nr 102/TT/2019”, uprzejmie informuję, że Urząd Miasta Bydgoszczy nie jest dysponentem wnioskowanych danych.",
            content);
        Assert.Contains(
            "art. 4 ust. 3 ustawy z dnia 6 września 2001 r. o dostępie do informacji publicznej (Dz. U. z 2022 r. poz. 902, z 2025 r. poz. 1844)",
            content);
        Assert.Contains("tj. Zarząd Dróg Miejskich i Komunikacji Publicznej w Bydgoszczy.", content);
        Assert.Contains(
            "Poniżej zamieszczam dane do kontaktu:\n\nZarząd Dróg Miejskich i Komunikacji Publicznej w Bydgoszczy\n" +
            "ul. Toruńska 174a\n85-844 Bydgoszcz\ne-mail: zarzad@zdmikp.bydgoszcz.pl\n\nŁączę wyrazy szacunku,",
            content);
        Assert.DoesNotContain(result.Draft.Redactions, r => r.Text.Contains("zdmikp", StringComparison.Ordinal));
    }
}
