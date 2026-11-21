using System.Net;
using System.Net.Http.Json;
using System.Text;
using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Features.Drafts;
using ClerkAssistant.Api.Features.Intake;
using ClerkAssistant.Api.Features.Knowledge;
using ClerkAssistant.Api.Features.Routing;
using ClerkAssistant.Api.Features.Search;
using ClerkAssistant.Api.Infrastructure.Ai;
using ClerkAssistant.Api.Tests.Infrastructure;
using DocumentFormat.OpenXml.Packaging;
using Microsoft.EntityFrameworkCore;

namespace ClerkAssistant.Api.Tests.Ai;

public class AiFeaturesTests(AiApiFactory factory) : IClassFixture<AiApiFactory>
{
    private const string SylwesterRequest = """
        Jan Kowalski
        Bydgoszcz, 15.11.2026 r.

        Wniosek o udostępnienie informacji publicznej

        Na podstawie ustawy z dnia 6 września 2001 r. o dostępie do informacji publicznej wnoszę o udostępnienie informacji dotyczących kosztów organizacji Sylwestra Miejskiego 2025.
        1. Jaki był łączny koszt koncertu sylwestrowego?
        2. Ile wyniosły honoraria artystów?

        Odpowiedź proszę przesłać na adres e-mail: jan.kowalski@example.com

        Z poważaniem
        Jan Kowalski
        """;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private DateOnly Today => DateOnly.FromDateTime(factory.Clock.Now.Date);

    [Fact]
    public async Task Intake_text_returns_heuristic_suggestion_deadline_case_number_and_history()
    {
        var past = await factory.AddRequestAsync(new NewRequest(
            "Koszty Sylwestra Miejskiego 2024",
            "Proszę o informację o kosztach organizacji koncertu sylwestrowego i honorariach artystów.",
            Today.AddMonths(-10))
        {
            Status = RequestStatus.ResponseSent,
            Departments = [new Assignment("BKB", Today.AddMonths(-10), Today.AddMonths(-10).AddDays(6))],
        });
        var client = await factory.ClientAsync();

        var response = await client.PostAsJsonAsync("/api/intake/text", new IntakeTextRequest(SylwesterRequest), ApiClient.Json, Ct);

        response.EnsureSuccessStatusCode();
        var result = (await response.Content.ReadFromJsonAsync<IntakeResult>(ApiClient.Json, Ct))!;
        Assert.Null(result.FileId);
        Assert.False(result.AiUsed);
        Assert.Empty(result.Warnings);
        Assert.Equal("Kosztów organizacji Sylwestra Miejskiego 2025", result.Suggestion.Subject);
        Assert.Equal("Jan Kowalski", result.Suggestion.RequesterName);
        Assert.Equal("jan.kowalski@example.com", result.Suggestion.RequesterContact);
        Assert.Equal(ReplyChannel.Email, result.Suggestion.ReplyChannel);
        Assert.Equal(new DateOnly(2026, 11, 15), result.Suggestion.RequestDate);
        Assert.Equal(2, result.Suggestion.RequestedItems.Length);
        Assert.True(result.Suggestion.LooksLikePublicInformationRequest);
        Assert.Equal(new DateOnly(2026, 11, 18), result.SuggestedReceivedOn);
        Assert.Equal(Deadlines.InitialDueDate(new DateOnly(2026, 11, 18)), result.SuggestedDueOn);
        Assert.Matches(@"^BKS\.IV\.1431\.\d+\.2026$", result.SuggestedCaseNumber);

        var similar = Assert.Single(result.SimilarCases, h => h.Id == past);
        Assert.Equal("Koszty Sylwestra Miejskiego 2024", similar.Title);
        Assert.Equal(SearchHitKind.Request, similar.Kind);
        Assert.Equal(["BKB"], similar.DepartmentSymbols);
        Assert.InRange(similar.Score, 0.01, 1);

        var suggestion = result.DepartmentSuggestions.First();
        Assert.Equal("BKB", suggestion.Symbol);
        Assert.InRange(suggestion.Confidence, 0.5, 1);
        Assert.Equal(SuggestionSource.History, suggestion.Source);
        Assert.Equal(1, suggestion.SimilarCaseCount);
        Assert.Equal(6, suggestion.AverageResponseDays);
        Assert.StartsWith("W 1 podobnej sprawie odpowiedzi udzielało to biuro (średnio w 6 dni).", suggestion.Reason);
        Assert.True(result.DepartmentSuggestions.Length <= DepartmentRouter.MaxSuggestions);
    }

    [Fact]
    public async Task Intake_file_stores_an_unattached_file_and_removes_stale_uploads()
    {
        var staleId = await factory.QueryAsync(async db =>
        {
            var file = new StoredFile
            {
                FileName = "stary.txt",
                ContentType = "text/plain",
                Content = [1],
                Sha256 = "x",
                CreatedAt = factory.Clock.GetUtcNow().AddDays(-2),
            };
            db.Files.Add(file);
            await db.SaveChangesAsync(Ct);
            return file.Id;
        });
        var client = await factory.ClientAsync();

        var response = await client.PostAsync("/api/intake", TestData.TextFile("wniosek.txt", SylwesterRequest), Ct);

        response.EnsureSuccessStatusCode();
        var result = (await response.Content.ReadFromJsonAsync<IntakeResult>(ApiClient.Json, Ct))!;
        Assert.NotNull(result.FileId);
        Assert.Equal("wniosek.txt", result.FileName);
        Assert.Contains("Sylwestra Miejskiego 2025", result.Text);
        Assert.True(await factory.QueryAsync(db => db.Files.AnyAsync(f => f.Id == result.FileId, Ct)));
        Assert.False(await factory.QueryAsync(db => db.Files.AnyAsync(f => f.Id == staleId, Ct)));
    }

    [Fact]
    public async Task Intake_rejects_unsupported_and_textless_files()
    {
        var client = await factory.ClientAsync();

        var unsupported = await client.PostAsync("/api/intake", TestData.TextFile("program.exe", "MZ"), Ct);
        var empty = await client.PostAsJsonAsync("/api/intake/text", new IntakeTextRequest("   "), ApiClient.Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, unsupported.StatusCode);
        Assert.Contains("\"file\"", await unsupported.Content.ReadAsStringAsync(Ct));
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
    }

    [Fact]
    public async Task Knowledge_upload_is_listed_searchable_downloadable_and_deletable()
    {
        const string ordinance = """
            Uchwała w sprawie regulaminu utrzymania czystości i porządku na terenie miasta

            § 1. Właściciele nieruchomości zapewniają utrzymanie czystości chodników przylegających do nieruchomości.

            § 2. Odpady komunalne odbierane są nie rzadziej niż raz na dwa tygodnie.
            """;
        var client = await factory.ClientAsync();

        var upload = await client.PostAsync(
            "/api/knowledge", TestData.TextFile("uchwala-czystosc.txt", ordinance, ("kind", "Ordinance")), Ct);

        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        var created = (await upload.Content.ReadFromJsonAsync<KnowledgeDocumentDto>(ApiClient.Json, Ct))!;
        Assert.Equal("uchwala-czystosc", created.Title);
        Assert.Equal(KnowledgeKind.Ordinance, created.Kind);
        Assert.Equal("Administrator", created.CreatedBy);

        var list = await client.GetFromJsonAsync<KnowledgeDocumentDto[]>("/api/knowledge", ApiClient.Json, Ct);
        var listed = Assert.Single(list!, d => d.Id == created.Id);
        Assert.True(listed.ChunkCount >= 1);
        Assert.False(listed.Indexed);

        var hits = await client.GetFromJsonAsync<SearchHit[]>("/api/search?q=regulamin%20czysto%C5%9B%C4%87", ApiClient.Json, Ct);
        var hit = Assert.Single(hits!, h => h.Id == created.Id);
        Assert.Equal(SearchHitKind.Knowledge, hit.Kind);
        Assert.Equal(KnowledgeKind.Ordinance, hit.KnowledgeKind);
        Assert.Contains("czystości", hit.Snippet);

        var onlyRequests = await client.GetFromJsonAsync<SearchHit[]>("/api/search?q=czysto%C5%9Bci&kind=Request", ApiClient.Json, Ct);
        Assert.DoesNotContain(onlyRequests!, h => h.Kind == SearchHitKind.Knowledge);

        var content = await client.GetAsync($"/api/knowledge/{created.Id}/content", Ct);
        Assert.Equal(ordinance, Encoding.UTF8.GetString(await content.Content.ReadAsByteArrayAsync(Ct)));

        var deleted = await client.DeleteAsync($"/api/knowledge/{created.Id}", Ct);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        var gone = await client.GetAsync($"/api/knowledge/{created.Id}/content", Ct);
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
        Assert.Equal(2, await factory.QueryAsync(db => db.AuditEntries.CountAsync(
            a => a.EntityId == created.Id && a.EntityType == nameof(KnowledgeDocument), Ct)));
    }

    [Fact]
    public async Task Knowledge_upload_rejects_invalid_kind()
    {
        var client = await factory.ClientAsync();

        var response = await client.PostAsync(
            "/api/knowledge", TestData.TextFile("a.txt", "Treść", ("kind", "Nieznany")), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("\"kind\"", await response.Content.ReadAsStringAsync(Ct));
    }

    [Theory]
    [InlineData("/api/search?q=", "q")]
    [InlineData("/api/search?q=remont&limit=0", "limit")]
    [InlineData("/api/search?q=remont&limit=51", "limit")]
    [InlineData("/api/search?q=remont&kind=1", "kind")]
    public async Task Search_validates_parameters(string url, string field)
    {
        var client = await factory.ClientAsync();

        var response = await client.GetAsync(url, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains($"\"{field}\"", await response.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task Search_finds_requests_by_subject_similarity_and_full_text()
    {
        var id = await factory.AddRequestAsync(new NewRequest(
            "Remont nawierzchni ulicy Gdańskiej",
            "Proszę o harmonogram i koszt remontu nawierzchni.",
            Today.AddDays(-3))
        {
            Departments = [new Assignment("WIM")],
        });
        var client = await factory.ClientAsync();

        var byPrefix = await client.GetFromJsonAsync<SearchHit[]>("/api/search?q=remontu%20gda%C5%84ska", ApiClient.Json, Ct);
        var byTypo = await client.GetFromJsonAsync<SearchHit[]>("/api/search?q=nawierzhni", ApiClient.Json, Ct);

        var hit = Assert.Single(byPrefix!, h => h.Id == id);
        Assert.Equal(SearchHitKind.Request, hit.Kind);
        Assert.Equal(["WIM"], hit.DepartmentSymbols);
        Assert.Equal(RequestStatus.NoAction, hit.Status);
        Assert.Contains(byTypo!, h => h.Id == id);
    }

    [Fact]
    public async Task Similar_returns_recent_cases_excluding_the_request_itself()
    {
        var target = await factory.AddRequestAsync(new NewRequest(
            "Wycinka drzew w parku Kochanowskiego", "Ile drzew wycięto w parku Kochanowskiego i dlaczego?", Today));
        var recent = await factory.AddRequestAsync(new NewRequest(
            "Drzewa w parku Kochanowskiego", "Proszę o informację o wycince drzew w parku Kochanowskiego.", Today.AddMonths(-3)));
        var old = await factory.AddRequestAsync(new NewRequest(
            "Wycinka drzew w parku Kochanowskiego 2024", "Wycinka drzew w parku Kochanowskiego.", Today.AddMonths(-14)));
        var client = await factory.ClientAsync();

        var hits = await client.GetFromJsonAsync<SearchHit[]>($"/api/requests/{target}/similar", ApiClient.Json, Ct);
        var missing = await client.GetAsync($"/api/requests/{Guid.NewGuid()}/similar", Ct);

        Assert.Contains(hits!, h => h.Id == recent);
        Assert.DoesNotContain(hits!, h => h.Id == target || h.Id == old);
        Assert.True(hits!.Length <= SearchEndpoints.SimilarCasesLimit);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Department_suggestions_without_ai_use_keywords_from_department_scope()
    {
        var id = await factory.AddRequestAsync(new NewRequest(
            "Rejestracja pojazdów i prawa jazdy",
            "Ile pojazdów zarejestrowano w 2025 roku i ile wydano praw jazdy?",
            Today));
        var client = await factory.ClientAsync();

        var suggestions = await client.GetFromJsonAsync<DepartmentSuggestion[]>(
            $"/api/requests/{id}/department-suggestions", ApiClient.Json, Ct);

        var first = suggestions!.First();
        Assert.Equal("WUK", first.Symbol);
        Assert.Equal(SuggestionSource.Keywords, first.Source);
        Assert.StartsWith("Zakres działania obejmuje:", first.Reason);
        Assert.True(first.Reason.Length <= DepartmentRouter.MaxReasonLength);
        Assert.InRange(first.Confidence, 0.01, 1);
    }

    [Fact]
    public async Task Draft_generate_save_detect_and_export_flow()
    {
        var id = await factory.AddRequestAsync(new NewRequest(
            "Koszty Sylwestra Miejskiego 2025", SylwesterRequest, new DateOnly(2026, 11, 16))
        {
            RequesterName = "Jan Kowalski",
            RequesterContact = "jan.kowalski@example.com",
        });
        await factory.AddDepartmentReplyAsync(id, "BKB",
            "Łączny koszt koncertu wyniósł 450 000 zł brutto.\nHonoraria artystów: 210 000 zł.\nOsoba do kontaktu: tel. 501 234 567.");
        var client = await factory.ClientAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/requests/{id}/draft", Ct)).StatusCode);

        var generated = await client.PostAsync($"/api/requests/{id}/draft/generate", null, Ct);
        generated.EnsureSuccessStatusCode();
        var result = (await generated.Content.ReadFromJsonAsync<GenerateDraftResult>(ApiClient.Json, Ct))!;
        var draft = result.Draft;
        Assert.False(result.AiUsed);
        Assert.False(draft.GeneratedByAi);
        Assert.Equal("Administrator", draft.UpdatedBy);
        Assert.StartsWith("Bydgoszcz, dnia 18 listopada 2026 r.\n\nZnak sprawy: BKS.IV.1431.", draft.Content);
        Assert.Contains("\n\nJan Kowalski\njan.kowalski@example.com\n\n", draft.Content);
        Assert.Contains("Dotyczy: wniosku o udostępnienie informacji publicznej z dnia 16 listopada 2026 r.", draft.Content);
        Assert.Contains("Informacja przekazana przez: Biuro Kultury Bydgoskiej", draft.Content);
        Assert.Contains("Łączny koszt koncertu wyniósł 450 000 zł brutto.", draft.Content);
        Assert.Contains("[DO UZUPEŁNIENIA:", draft.Content);
        Assert.EndsWith("Z poważaniem\n\n[podpis osoby upoważnionej]", draft.Content);
        Assert.Contains(draft.Redactions, r => r.Category == RedactionCategory.PersonName && r.Text == "Jan Kowalski");
        Assert.Contains(draft.Redactions, r => r.Category == RedactionCategory.Email);
        Assert.Contains(draft.Redactions, r => r.Category == RedactionCategory.Phone && r.Text == "501 234 567");
        Assert.All(draft.Redactions, r => Assert.Equal(RedactionStatus.Suggested, r.Status));
        Assert.All(draft.Redactions, r => Assert.Equal(r.Text, draft.Content.Substring(r.Start, r.Length)));

        var fetched = await client.GetFromJsonAsync<DraftDto>($"/api/requests/{id}/draft", ApiClient.Json, Ct);
        Assert.Equal(draft.Version, fetched!.Version);

        var email = draft.Redactions.First(r => r.Category == RedactionCategory.Email);
        var invalid = new Redaction(0, 9, "Nieistnie", RedactionCategory.Other, RedactionStatus.Approved, RedactionSource.Manual);
        var saved = await client.PutAsJsonAsync($"/api/requests/{id}/draft",
            new SaveDraftRequest(draft.Content, [email with { Status = RedactionStatus.Approved }, invalid], draft.Version),
            ApiClient.Json, Ct);
        saved.EnsureSuccessStatusCode();
        var savedDraft = (await saved.Content.ReadFromJsonAsync<DraftDto>(ApiClient.Json, Ct))!;
        var kept = Assert.Single(savedDraft.Redactions);
        Assert.Equal(RedactionStatus.Approved, kept.Status);
        Assert.NotEqual(draft.Version, savedDraft.Version);

        var stale = await client.PutAsJsonAsync($"/api/requests/{id}/draft",
            new SaveDraftRequest(draft.Content + " zmiana", [], draft.Version), ApiClient.Json, Ct);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        var edited = "Nowy wstęp.\n\n" + savedDraft.Content;
        var detected = await client.PostAsJsonAsync($"/api/requests/{id}/draft/detect",
            new DetectRedactionsRequest(edited, savedDraft.Redactions), ApiClient.Json, Ct);
        detected.EnsureSuccessStatusCode();
        var detection = (await detected.Content.ReadFromJsonAsync<DetectRedactionsResult>(ApiClient.Json, Ct))!;
        Assert.Contains(detection.Redactions, r => r.Status == RedactionStatus.Approved && r.Start == kept.Start + 13);
        Assert.Contains(detection.Redactions, r => r is { Status: RedactionStatus.Suggested, Category: RedactionCategory.Phone });
        Assert.All(detection.Redactions, r => Assert.Equal(r.Text, edited.Substring(r.Start, r.Length)));
        Assert.False(detection.AiUsed);
        Assert.Empty(detection.Warnings);

        var txt = await client.GetAsync($"/api/requests/{id}/draft/export?format=txt", Ct);
        txt.EnsureSuccessStatusCode();
        var text = await txt.Content.ReadAsStringAsync(Ct);
        Assert.Contains("[dane zanonimizowane]", text);
        Assert.DoesNotContain("jan.kowalski@example.com", text);
        Assert.EndsWith(DraftExport.AnonymizationNotice, text);
        Assert.Matches(@"^BKS-IV-1431-\d+-2026-odpowiedz\.txt$", txt.Content.Headers.ContentDisposition!.FileNameStar);
        Assert.StartsWith("text/plain", txt.Content.Headers.ContentType!.ToString());

        var docx = await client.GetAsync($"/api/requests/{id}/draft/export", Ct);
        docx.EnsureSuccessStatusCode();
        Assert.Equal(DraftExport.DocxContentType, docx.Content.Headers.ContentType!.MediaType);
        using (var document = WordprocessingDocument.Open(new MemoryStream(await docx.Content.ReadAsByteArrayAsync(Ct)), false))
        {
            var body = document.MainDocumentPart!.Document!.Body!.InnerText;
            Assert.Contains("[dane zanonimizowane]", body);
            Assert.Contains("Łączny koszt koncertu wyniósł 450 000 zł brutto.", body);
        }

        var badFormat = await client.GetAsync($"/api/requests/{id}/draft/export?format=pdf", Ct);
        Assert.Equal(HttpStatusCode.BadRequest, badFormat.StatusCode);

        var actions = await factory.QueryAsync(db => db.AuditEntries
            .Where(a => a.EntityId == id && a.EntityType == nameof(InfoRequest))
            .Select(a => a.Action)
            .ToListAsync(Ct));
        Assert.Equal(["draft.exported", "draft.exported", "draft.generated", "draft.saved"], actions.Order());
    }

    [Fact]
    public async Task Draft_generation_without_department_replies_returns_template_with_warning()
    {
        var id = await factory.AddRequestAsync(new NewRequest("Liczba etatów w urzędzie", "Ile etatów jest w urzędzie?", Today));
        var client = await factory.ClientAsync();

        var response = await client.PostAsync($"/api/requests/{id}/draft/generate", null, Ct);

        response.EnsureSuccessStatusCode();
        var result = (await response.Content.ReadFromJsonAsync<GenerateDraftResult>(ApiClient.Json, Ct))!;
        Assert.Contains(DraftComposer.NoRepliesWarning, result.Warnings);
        Assert.DoesNotContain(AiGateway.FallbackWarning, result.Warnings);
        Assert.Contains("\n\nWnioskodawca\n\n", result.Draft.Content);
        Assert.Contains("[DO UZUPEŁNIENIA: treść odpowiedzi na wniosek]", result.Draft.Content);
        Assert.Equal(RequestStatus.NoAction, await factory.QueryAsync(db =>
            db.Requests.Where(r => r.Id == id).Select(r => r.Status).SingleAsync(Ct)));
    }

    [Fact]
    public async Task Draft_endpoints_return_not_found_for_unknown_request()
    {
        var client = await factory.ClientAsync();
        var unknown = Guid.NewGuid();

        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"/api/requests/{unknown}/draft/generate", null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/requests/{unknown}/draft/export", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync(
            $"/api/requests/{unknown}/draft", new SaveDraftRequest("x", [], null), ApiClient.Json, Ct)).StatusCode);
    }
}
