using System.Net.Http.Json;
using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Features.Requests;
using ClerkAssistant.Api.Tests.Infrastructure;
using static ClerkAssistant.Api.Tests.Register.RegisterApi;

namespace ClerkAssistant.Api.Tests.Register;

public class RegisterListTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task List_carries_over_open_requests_from_previous_year_and_orders_by_deadline()
    {
        factory.SetToday(new DateOnly(2026, 1, 8));
        var client = await factory.CreateAuthenticatedClientAsync();

        var openFromLastYear = await client.CreateRequestAsync("Grudniowy wniosek bez odpowiedzi", new DateOnly(2025, 12, 15));
        var closedFromLastYear = await client.CreateRequestAsync("Grudniowy wniosek zamknięty", new DateOnly(2025, 12, 10));
        var dueLater = await client.CreateRequestAsync("Styczniowy wniosek późniejszy", new DateOnly(2026, 1, 7));
        var dueSooner = await client.CreateRequestAsync("Styczniowy wniosek wcześniejszy", new DateOnly(2026, 1, 2));
        var closedEarlier = await client.CreateRequestAsync("Styczniowy wniosek zamknięty wcześniej", new DateOnly(2026, 1, 5));
        var closedLater = await client.CreateRequestAsync("Styczniowy wniosek zamknięty później", new DateOnly(2026, 1, 2));
        await client.ChangeStatusAsync(closedFromLastYear, RequestStatus.ResponseSent, new DateOnly(2026, 1, 5));
        await client.ChangeStatusAsync(closedEarlier, RequestStatus.ResponseSent, new DateOnly(2026, 1, 7));
        await client.ChangeStatusAsync(closedLater, RequestStatus.ResponseSent);

        var current = (await client.GetFromJsonAsync<RequestListItem[]>("/api/requests?year=2026", ApiClient.Json, Ct))!;
        var defaultYear = await client.GetFromJsonAsync<RequestListItem[]>("/api/requests", ApiClient.Json, Ct);
        var previous = await client.GetFromJsonAsync<RequestListItem[]>("/api/requests?year=2025", ApiClient.Json, Ct);

        Assert.Equal([openFromLastYear, dueSooner, dueLater, closedLater, closedEarlier], current.Select(r => r.Id));
        Assert.Equal(current.Select(r => r.Id), defaultYear!.Select(r => r.Id));
        Assert.Equal([openFromLastYear, closedFromLastYear], previous!.Select(r => r.Id));

        var carriedOver = current[0];
        Assert.Equal(new DateOnly(2025, 12, 29), carriedOver.EffectiveDueOn);
        Assert.Equal(Urgency.Overdue, carriedOver.Urgency);
        Assert.Equal(-10, carriedOver.DaysLeft);
        Assert.Equal("BKS.IV.1431.1.2025", carriedOver.CaseNumber);
        Assert.Equal(new DateOnly(2026, 1, 8), current[3].RespondedOn);
        Assert.Equal(Urgency.Closed, current[3].Urgency);
    }

    [Fact]
    public async Task Summary_and_years_cover_the_register_of_the_year()
    {
        factory.SetToday(new DateOnly(2026, 1, 8));
        var client = await factory.CreateAuthenticatedClientAsync();
        var list = await client.GetFromJsonAsync<RequestListItem[]>("/api/requests?year=2026", ApiClient.Json, Ct);

        var summary = await client.GetFromJsonAsync<RegisterSummary>("/api/requests/summary?year=2026", ApiClient.Json, Ct);
        var years = await client.GetFromJsonAsync<int[]>("/api/requests/years", ApiClient.Json, Ct);

        Assert.Equal(list!.Length, summary!.Total);
        Assert.Equal(list.Count(r => r.Status != RequestStatus.ResponseSent), summary.Open);
        Assert.Equal(list.Count(r => r.Urgency == Urgency.Overdue), summary.Overdue);
        Assert.Equal(summary.Total, summary.Open + summary.Closed);
        Assert.Equal(2026, years![0]);
        Assert.Equal(years.OrderDescending(), years);
    }

    [Fact]
    public async Task Deadline_preview_applies_statutory_rules()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var preview = await client.GetFromJsonAsync<DeadlinePreview>(
            "/api/requests/deadline-preview?receivedOn=2026-12-10", ApiClient.Json, Ct);

        Assert.Equal(new DeadlinePreview(new DateOnly(2026, 12, 28), new DateOnly(2027, 2, 10)), preview);
    }
}

public class RegisterSearchTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Search_escapes_wildcards_and_filters_combine()
    {
        factory.SetToday(new DateOnly(2026, 11, 18));
        var client = await factory.CreateAuthenticatedClientAsync();
        var percent = await client.CreateRequestAsync("Realizacja budżetu w 100% w 2025 r.", new DateOnly(2026, 11, 16));
        var thousand = await client.CreateRequestAsync("Nasadzenia 1000 drzew", new DateOnly(2026, 11, 2));
        await client.CreateRequestAsync(NewRequest("Wniosek Kowalskiego", new DateOnly(2026, 11, 2)) with { RequesterName = "Adam Kowalski" });

        var byPercent = await client.GetFromJsonAsync<RequestListItem[]>("/api/requests?search=100%25", ApiClient.Json, Ct);
        var byDigits = await client.GetFromJsonAsync<RequestListItem[]>("/api/requests?search=100", ApiClient.Json, Ct);
        var byRequester = await client.GetFromJsonAsync<RequestListItem[]>("/api/requests?search=kowalski", ApiClient.Json, Ct);
        var byCaseNumber = await client.GetFromJsonAsync<RequestListItem[]>("/api/requests?search=1431.2.2026", ApiClient.Json, Ct);
        var byDueDate = await client.GetFromJsonAsync<RequestListItem[]>("/api/requests?dueOn=2026-11-16", ApiClient.Json, Ct);
        var byStatus = await client.GetFromJsonAsync<RequestListItem[]>("/api/requests?status=QueriesSent", ApiClient.Json, Ct);
        var byAssignee = await client.GetFromJsonAsync<RequestListItem[]>(
            $"/api/requests?assigneeId={Guid.NewGuid()}", ApiClient.Json, Ct);

        Assert.Equal([percent], byPercent!.Select(r => r.Id));
        Assert.Equal([thousand, percent], byDigits!.Select(r => r.Id));
        Assert.Equal("Adam Kowalski", Assert.Single(byRequester!).RequesterName);
        Assert.Equal([thousand], byCaseNumber!.Select(r => r.Id));
        Assert.Equal(2, byDueDate!.Length);
        Assert.Empty(byStatus!);
        Assert.Empty(byAssignee!);
    }
}
