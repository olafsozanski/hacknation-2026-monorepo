using System.Net.Http.Json;
using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Features.Requests;
using ClerkAssistant.Api.Infrastructure.Persistence;
using ClerkAssistant.Api.Infrastructure.Seeding;
using ClerkAssistant.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static ClerkAssistant.Api.Tests.Register.RegisterApi;

namespace ClerkAssistant.Api.Tests.Register;

public sealed class DemoDataFactory : ApiFactory
{
    public const string DemoPassword = "Urzednik12345";

    public DemoDataFactory() => Clock.Now = new DateTimeOffset(2026, 11, 21, 8, 0, 0, TimeSpan.FromHours(1));

    protected override IReadOnlyDictionary<string, string?> Settings => new Dictionary<string, string?>
    {
        ["Seed:DemoData"] = "true",
        ["Seed:DemoPassword"] = DemoPassword,
    };
}

public class DemoDataSeederTests(DemoDataFactory factory) : IClassFixture<DemoDataFactory>
{
    [Fact]
    public async Task Demo_register_has_a_realistic_spread_of_deadlines()
    {
        var client = await factory.CreateAuthenticatedClientAsync("akowalska", DemoDataFactory.DemoPassword);

        var summary = await client.GetFromJsonAsync<RegisterSummary>("/api/requests/summary", ApiClient.Json, Ct);
        var list = (await client.GetFromJsonAsync<RequestListItem[]>("/api/requests", ApiClient.Json, Ct))!;

        Assert.Equal(new RegisterSummary(1, 2, 1, 2, 10, summary!.Closed, 10 + summary.Closed), summary);
        Assert.True(summary.Closed > 0);
        var open = list.Where(r => r.Status != RequestStatus.ResponseSent).ToList();
        Assert.Equal(["Anna Kowalska", "Maria Wiśniewska", "Piotr Nowak"], open.Select(r => r.Assignee!.FullName).Distinct().Order());
        Assert.Contains(open, r => r.Status == RequestStatus.Extended && r.ExtensionDays is not null);
        Assert.Equal(open.Select(r => r.EffectiveDueOn).Order(), open.Select(r => r.EffectiveDueOn));
    }

    [Fact]
    public async Task Demo_requests_have_department_replies_and_a_prepared_draft()
    {
        var client = await factory.CreateAuthenticatedClientAsync("pnowak", DemoDataFactory.DemoPassword);
        var list = (await client.GetFromJsonAsync<RequestListItem[]>("/api/requests", ApiClient.Json, Ct))!;

        var details = new List<RequestDetail>();
        foreach (var item in list.Where(r => r.Status != RequestStatus.ResponseSent))
        {
            details.Add(await client.GetRequestAsync(item.Id));
        }

        var prepared = Assert.Single(details, d => d.Status == RequestStatus.ResponsePrepared);
        Assert.NotNull(prepared.Draft);
        Assert.True(details.Count(d => d.Documents.Any(doc => doc.Kind == DocumentKind.DepartmentReply && doc.HasText)) >= 2);
        Assert.All(details, d => Assert.True(d.ReceivedOn <= new DateOnly(2026, 11, 21)));
        Assert.All(details.SelectMany(d => d.Departments), l => Assert.True(l.AnsweredOn is null || l.AnsweredOn >= l.AskedOn));
        var history = await client.GetFromJsonAsync<HistoryItem[]>($"/api/requests/{prepared.Id}/history", ApiClient.Json, Ct);
        Assert.Contains(history!, h => h.Action == "request.created");
    }

    [Fact]
    public async Task Seeding_is_idempotent_and_archive_is_complete()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var before = await db.Requests.CountAsync(Ct);

        await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().SeedAsync();

        Assert.Equal(before, await db.Requests.CountAsync(Ct));
        var archived = await db.Requests.Include(r => r.Draft).Include(r => r.Departments)
            .Where(r => r.Status == RequestStatus.ResponseSent).ToListAsync(Ct);
        Assert.InRange(archived.Count, 18, 25);
        Assert.All(archived, r =>
        {
            Assert.NotNull(r.RespondedOn);
            Assert.True(r.RespondedOn <= r.EffectiveDueOn);
            Assert.Contains(r.CaseNumber, r.Draft!.Content);
            Assert.NotEmpty(r.Departments);
            Assert.Null(r.Embedding);
        });
        Assert.Equal(before, await db.Requests.Select(r => r.CaseNumber).Distinct().CountAsync(Ct));
    }
}
