using System.Net;
using System.Net.Http.Json;
using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Features.Requests;
using ClerkAssistant.Api.Tests.Infrastructure;
using static ClerkAssistant.Api.Tests.Register.RegisterApi;

namespace ClerkAssistant.Api.Tests.Register;

public class RequestWorkflowTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly DateOnly Today = new(2026, 11, 18);

    [Fact]
    public async Task Extension_beyond_two_months_from_filing_is_rejected()
    {
        factory.SetToday(Today);
        var client = await factory.CreateAuthenticatedClientAsync();
        var id = await client.CreateRequestAsync("Wykaz umów na odśnieżanie", new DateOnly(2026, 11, 16));
        var version = (await client.GetRequestAsync(id)).Version;

        var tooLong = await client.PostAsJsonAsync($"/api/requests/{id}/extension",
            new ExtendDeadlineRequest(62, "Konieczność kwerendy archiwalnej", version), ApiClient.Json, Ct);
        var errors = await tooLong.ValidationErrorsAsync();

        Assert.Contains("18.01.2027", Assert.Single(errors["days"]));
    }

    [Fact]
    public async Task Extension_sets_new_deadline_and_returns_formal_notice()
    {
        factory.SetToday(Today);
        var client = await factory.CreateAuthenticatedClientAsync();
        var id = await client.CreateRequestAsync("Wykaz umów na zimowe utrzymanie dróg", new DateOnly(2026, 11, 16));
        var before = await client.GetRequestAsync(id);

        var response = await client.PostAsJsonAsync($"/api/requests/{id}/extension",
            new ExtendDeadlineRequest(30, "Konieczność kwerendy archiwalnej w trzech wydziałach", before.Version), ApiClient.Json, Ct);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ExtensionResult>(ApiClient.Json, Ct);
        var after = await client.GetRequestAsync(id);

        Assert.Equal(new DateOnly(2026, 12, 30), result!.ExtendedDueOn);
        Assert.StartsWith(
            $"URZĄD MIASTA BYDGOSZCZY\nBiuro Komunikacji Społecznej\n\nBydgoszcz, dnia 18 listopada 2026 r.\n\n{before.CaseNumber}\n\nSzanowny Panie,",
            result.NoticeText);
        Assert.Contains("„Wykaz umów na zimowe utrzymanie dróg”, ze względu na konieczność kwerendy archiwalnej w trzech wydziałach powiadamiam",
            result.NoticeText);
        Assert.Contains("termin udzielenia odpowiedzi: 30 grudnia 2026 r.", result.NoticeText);
        Assert.DoesNotContain("WEZWANIE", result.NoticeText);
        Assert.Equal(RequestStatus.Extended, after.Status);
        Assert.Equal(ExtensionKind.Query, after.ExtensionKind);
        Assert.Equal(new DateOnly(2026, 12, 30), after.EffectiveDueOn);
        Assert.Equal(30, after.ExtensionDays);
    }

    [Fact]
    public async Task Clarification_extension_returns_call_to_clarify_and_is_audited_as_such()
    {
        factory.SetToday(Today);
        var client = await factory.CreateAuthenticatedClientAsync();
        var id = await client.CreateRequestAsync(NewRequest("Dokumentacja budowy hali", new DateOnly(2026, 11, 16)) with { RequesterName = "Ewa Górna" });
        var before = await client.GetRequestAsync(id);

        var response = await client.PostAsJsonAsync($"/api/requests/{id}/extension",
            new ExtendDeadlineRequest(30, "proszę wskazać, o które akta sprawy chodzi", before.Version, ExtensionKind.Clarification),
            ApiClient.Json, Ct);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ExtensionResult>(ApiClient.Json, Ct);
        var after = await client.GetRequestAsync(id);
        var history = await client.GetFromJsonAsync<HistoryItem[]>($"/api/requests/{id}/history", ApiClient.Json, Ct);

        Assert.Contains("WEZWANIE DO DOPRECYZOWANIA\nZAKRESU OTRZYMANEGO WNIOSKU\n\nSzanowna Pani,", result!.NoticeText);
        Assert.Contains("Proszę wskazać, o które akta sprawy chodzi.", result.NoticeText);
        Assert.Contains("wskazując dzień 30 grudnia 2026 r.", result.NoticeText);
        Assert.Equal(ExtensionKind.Clarification, after.ExtensionKind);
        Assert.Equal("request.extended", history![0].Action);
        Assert.Contains("wezwanie do doprecyzowania", history[0].Details);
    }

    [Fact]
    public async Task Stale_version_is_rejected_with_conflict()
    {
        factory.SetToday(Today);
        var client = await factory.CreateAuthenticatedClientAsync();
        var id = await client.CreateRequestAsync("Koszt kampanii promocyjnej", new DateOnly(2026, 11, 10));
        var original = await client.GetRequestAsync(id);

        var first = await client.PutAsJsonAsync($"/api/requests/{id}", ToUpdate(original, "Koszt kampanii promocyjnej miasta"), ApiClient.Json, Ct);
        first.EnsureSuccessStatusCode();
        var updated = await first.Content.ReadFromJsonAsync<RequestDetail>(ApiClient.Json, Ct);
        var stalePut = await client.PutAsJsonAsync($"/api/requests/{id}", ToUpdate(original, "Inny przedmiot"), ApiClient.Json, Ct);
        var staleStatus = await client.PostAsJsonAsync($"/api/requests/{id}/status",
            new ChangeStatusRequest(RequestStatus.ResponsePrepared, null, original.Version), ApiClient.Json, Ct);

        Assert.Equal("Koszt kampanii promocyjnej miasta", updated!.Subject);
        Assert.NotEqual(original.Version, updated.Version);
        Assert.Equal(HttpStatusCode.Conflict, stalePut.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, staleStatus.StatusCode);
        Assert.Equal("Koszt kampanii promocyjnej miasta", (await client.GetRequestAsync(id)).Subject);
    }

    [Fact]
    public async Task Department_query_asks_for_reply_three_working_days_before_deadline()
    {
        factory.SetToday(Today);
        var client = await factory.CreateAuthenticatedClientAsync();
        var wgk = await client.DepartmentIdAsync("WGK");
        var roomy = await client.CreateRequestAsync(NewRequest("Liczba koszy na śmieci", new DateOnly(2026, 11, 16), departmentIds: [wgk]));
        var tight = await client.CreateRequestAsync("Koszt sprzątania po festynie", new DateOnly(2026, 11, 5));

        var roomyQuery = await client.GetFromJsonAsync<DepartmentQueryDto>($"/api/requests/{roomy}/department-query", ApiClient.Json, Ct);
        var tightQuery = await client.GetFromJsonAsync<DepartmentQueryDto>($"/api/requests/{tight}/department-query", ApiClient.Json, Ct);
        var roomyDetail = await client.GetRequestAsync(roomy);

        Assert.Equal(new DateOnly(2026, 11, 25), roomyQuery!.ReplyBy);
        Assert.Matches(@"^BKS\.IV\.1431\.\d+\.2026$", roomyDetail.CaseNumber);
        Assert.Contains(roomyDetail.CaseNumber, roomyQuery.Subject);
        Assert.Contains("do 25 listopada 2026 r.", roomyQuery.Subject);
        Assert.EndsWith("Łączę wyrazy szacunku,\nBiuro Komunikacji Społecznej\nUrząd Miasta Bydgoszczy", roomyQuery.Body);
        Assert.Contains("Wydział Zieleni i Gospodarki Komunalnej (WGK)", roomyQuery.Body);
        Assert.Contains("„Liczba koszy na śmieci”", roomyQuery.Body);
        Assert.Contains("do dnia 25 listopada 2026 r.", roomyQuery.Body);
        Assert.Contains("30 listopada 2026 r.", roomyQuery.Body);
        Assert.Contains(roomyDetail.Content, roomyQuery.Body);
        Assert.Contains("Biuro Komunikacji Społecznej", roomyQuery.Body);
        Assert.Equal(Today, tightQuery!.ReplyBy);
    }

    [Fact]
    public async Task Department_dates_are_tracked_and_asking_moves_status_to_queries_sent()
    {
        factory.SetToday(Today);
        var client = await factory.CreateAuthenticatedClientAsync();
        var wgk = await client.DepartmentIdAsync("WGK");
        var wos = await client.DepartmentIdAsync("WOŚ");
        var id = await client.CreateRequestAsync(NewRequest("Wycinka drzew w parku", new DateOnly(2026, 11, 12), departmentIds: [wgk]));

        var asked = await client.PatchAsJsonAsync($"/api/requests/{id}/departments/{wgk}",
            new UpdateRequestDepartmentRequest(new DateOnly(2026, 11, 13), null), ApiClient.Json, Ct);
        var set = await client.PutAsJsonAsync($"/api/requests/{id}/departments",
            new SetDepartmentsRequest([wos, wgk]), ApiClient.Json, Ct);
        var invalid = await client.PatchAsJsonAsync($"/api/requests/{id}/departments/{wos}",
            new UpdateRequestDepartmentRequest(new DateOnly(2026, 11, 17), new DateOnly(2026, 11, 16)), ApiClient.Json, Ct);
        var links = (await set.Content.ReadFromJsonAsync<RequestDepartmentDto[]>(ApiClient.Json, Ct))!;
        var detail = await client.GetRequestAsync(id);

        asked.EnsureSuccessStatusCode();
        Assert.Equal(RequestStatus.QueriesSent, detail.Status);
        Assert.Equal(["WGK", "WOŚ"], links.Select(l => l.Symbol));
        Assert.Equal(new DateOnly(2026, 11, 13), links.Single(l => l.Symbol == "WGK").AskedOn);
        Assert.Null(links.Single(l => l.Symbol == "WOŚ").AskedOn);
        Assert.Contains("answeredOn", (await invalid.ValidationErrorsAsync()).Keys);
    }

    [Fact]
    public async Task History_records_actions_newest_first()
    {
        factory.SetToday(Today);
        var client = await factory.CreateAuthenticatedClientAsync();
        var id = await client.CreateRequestAsync("Liczba wydanych zezwoleń na alkohol", new DateOnly(2026, 11, 16));
        factory.Clock.Now = factory.Clock.Now.AddMinutes(1);
        var original = await client.GetRequestAsync(id);
        (await client.PutAsJsonAsync($"/api/requests/{id}", ToUpdate(original, "Liczba zezwoleń na sprzedaż alkoholu"), ApiClient.Json, Ct))
            .EnsureSuccessStatusCode();
        factory.Clock.Now = factory.Clock.Now.AddMinutes(1);
        await client.ChangeStatusAsync(id, RequestStatus.ResponsePrepared);

        var history = (await client.GetFromJsonAsync<HistoryItem[]>($"/api/requests/{id}/history", ApiClient.Json, Ct))!;

        Assert.Equal(["request.status-changed", "request.updated", "request.created"], history.Select(h => h.Action));
        Assert.All(history, h => Assert.Equal("Administrator", h.UserName));
        Assert.Equal("Status: bez działań → odpowiedź przygotowana", history[0].Details);
        Assert.Equal("Zmieniono: przedmiot", history[1].Details);
        Assert.Equal(original.CaseNumber, history[2].Details);
        Assert.True(history[0].At > history[1].At);
    }

    [Fact]
    public async Task Unknown_resources_return_not_found_problems()
    {
        factory.SetToday(Today);
        var client = await factory.CreateAuthenticatedClientAsync();
        var id = await client.CreateRequestAsync("Wniosek bez wydziałów", Today);

        var responses = new[]
        {
            await client.GetAsync($"/api/requests/{Guid.NewGuid()}", Ct),
            await client.PostAsJsonAsync($"/api/requests/{Guid.NewGuid()}/extension",
                new ExtendDeadlineRequest(10, "Kwerenda", 1), ApiClient.Json, Ct),
            await client.PatchAsJsonAsync($"/api/requests/{id}/departments/{Guid.NewGuid()}",
                new UpdateRequestDepartmentRequest(Today, null), ApiClient.Json, Ct),
            await client.GetAsync($"/api/documents/{Guid.NewGuid()}/text", Ct),
            await client.DeleteAsync($"/api/documents/{Guid.NewGuid()}", Ct),
        };

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.NotFound, r.StatusCode));
        var problem = await responses[0].Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>(ApiClient.Json, Ct);
        Assert.Equal("Nie znaleziono wniosku.", problem!.Detail);
    }

    private static UpdateRequestRequest ToUpdate(RequestDetail detail, string subject) => new(
        subject,
        detail.Content,
        detail.RequesterName,
        detail.RequesterContact,
        detail.ReplyChannel,
        detail.ReceivedOn,
        detail.DueOn,
        detail.CaseNumber,
        detail.Assignee?.Id,
        detail.Version);
}
