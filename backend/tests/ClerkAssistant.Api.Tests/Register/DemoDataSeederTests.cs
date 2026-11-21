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
        var fictionalArchive = await db.Requests.Include(r => r.Draft).Include(r => r.Departments)
            .Where(r => r.Status == RequestStatus.ResponseSent && !SampleCaseNumbers.Contains(r.CaseNumber))
            .ToListAsync(Ct);
        Assert.InRange(fictionalArchive.Count, 18, 25);
        Assert.All(fictionalArchive, r =>
        {
            Assert.NotNull(r.RespondedOn);
            Assert.True(r.RespondedOn <= r.EffectiveDueOn);
            Assert.StartsWith($"Bydgoszcz, dnia {r.RespondedOn!.Value.Day} ", r.Draft!.Content);
            Assert.Contains($"\n\n{r.CaseNumber}\n\n", r.Draft.Content);
            Assert.EndsWith("Łączę wyrazy szacunku,\nBiuro Komunikacji Społecznej\nUrząd Miasta Bydgoszczy", r.Draft.Content);
            Assert.NotEmpty(r.Departments);
        });
    }

    [Fact]
    public async Task Sample_cases_keep_their_real_case_numbers_and_letters()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var samples = await db.Requests
            .Include(r => r.Draft).Include(r => r.Documents).ThenInclude(d => d.File).Include(r => r.Departments).Include(r => r.Assignee)
            .Where(r => SampleCaseNumbers.Contains(r.CaseNumber))
            .ToDictionaryAsync(r => r.CaseNumber, Ct);

        Assert.Equal(SampleCaseNumbers.Order(), samples.Keys.Order());
        Assert.All(samples.Values, r =>
        {
            var parts = r.CaseNumber.Split('.');
            Assert.Equal(int.Parse(parts[^2]), r.SequenceNumber);
            Assert.Equal(int.Parse(parts[^1]), r.Year);
            Assert.Equal(RequestStatus.ResponseSent, r.Status);
            Assert.NotNull(r.RespondedOn);
            Assert.All(r.Departments, l => Assert.True(l.AskedOn >= r.ReceivedOn && l.AnsweredOn >= l.AskedOn && l.AnsweredOn <= r.RespondedOn));
            Assert.Contains(r.Assignee!.UserName, new[] { "akowalska", "pnowak" });
            Assert.Empty(r.Draft?.Redactions ?? []);
        });

        var query = samples["BKS.IV.1431.543.2026"];
        Assert.Equal(ExtensionKind.Query, query.ExtensionKind);
        Assert.Equal(new DateOnly(2026, 9, 25), query.ExtendedDueOn);
        Assert.Equal(28, query.ExtensionDays);
        Assert.Null(query.Draft);
        Assert.Equal(query.Subject, query.Content);
        var queryLetter = Assert.Single(query.Documents);
        Assert.Equal(DocumentKind.Attachment, queryLetter.Kind);
        Assert.Equal("Kwerenda - BKS.IV.1431.543.2026.txt", queryLetter.File.FileName);
        Assert.Contains("ostateczna odpowiedź nastąpi w terminie późniejszym", queryLetter.File.ExtractedText);

        var clarification = samples["BKS.IV.1431.509.2026"];
        Assert.Equal(ExtensionKind.Clarification, clarification.ExtensionKind);
        Assert.Equal("Doprecyzowanie - BKS.IV.1431.509.2026.txt", Assert.Single(clarification.Documents).File.FileName);
        Assert.Null(clarification.Draft);
        Assert.Null(samples["BKS.IV.1431.599.2026"].Draft);

        Assert.StartsWith("Bydgoszcz, dnia 14 stycznia 2026 r.", samples["BKS.IV.1431.15.2026"].Draft!.Content);
        Assert.Equal("Antoni Ptak, Stowarzyszenie Obrony Zwierzątek", samples["BKS.IV.1431.293.2025"].RequesterName);
        Assert.Equal("abcxyz@jahu.komm", samples["BKS.IV.1431.17.2026"].RequesterContact);
    }

    [Fact]
    public async Task Fictional_requests_are_numbered_after_the_highest_real_sequence()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var all = await db.Requests.Select(r => new { r.Year, r.SequenceNumber, r.CaseNumber, r.Status }).ToListAsync(Ct);
        var fictional = all.Where(r => !SampleCaseNumbers.Contains(r.CaseNumber)).ToList();

        Assert.Equal(all.Count, all.Select(r => (r.Year, r.SequenceNumber)).Distinct().Count());
        Assert.All(fictional, r => Assert.Equal($"BKS.IV.1431.{r.SequenceNumber}.{r.Year}", r.CaseNumber));
        Assert.All(fictional.Where(r => r.Year == 2026), r => Assert.True(r.SequenceNumber > 599));
        Assert.All(fictional.Where(r => r.Year == 2025), r => Assert.True(r.SequenceNumber > 307));
        var open = fictional.Where(r => r.Status != RequestStatus.ResponseSent).Select(r => r.SequenceNumber).ToList();
        var archived = fictional.Where(r => r.Status == RequestStatus.ResponseSent && r.Year == 2026).Select(r => r.SequenceNumber).ToList();
        Assert.True(open.Min() > archived.Max());
    }

    internal static readonly string[] SampleCaseNumbers =
    [
        "BKS.IV.1431.293.2025", "BKS.IV.1431.307.2025", "BKS.IV.1431.15.2026", "BKS.IV.1431.16.2026", "BKS.IV.1431.17.2026",
        "BKS.IV.1431.18.2026", "BKS.IV.1431.509.2026", "BKS.IV.1431.543.2026", "BKS.IV.1431.590.2026", "BKS.IV.1431.599.2026",
    ];
}

public class DemoDataNumberingTests(DemoDataFactory factory) : IClassFixture<DemoDataFactory>
{
    [Fact]
    public async Task New_request_continues_after_the_last_demo_case_number()
    {
        var client = await factory.CreateAuthenticatedClientAsync("mwisniewska", DemoDataFactory.DemoPassword);
        var list = (await client.GetFromJsonAsync<RequestListItem[]>("/api/requests?year=2026", ApiClient.Json, Ct))!;
        var highest = list.Where(r => r.CaseNumber.EndsWith(".2026", StringComparison.Ordinal)).Max(r => r.SequenceNumber);

        var created = await client.GetRequestAsync(await client.CreateRequestAsync("Nowy wniosek po danych demonstracyjnych", new DateOnly(2026, 11, 20)));

        Assert.True(highest > 599);
        Assert.Equal(highest + 1, created.SequenceNumber);
        Assert.Equal($"BKS.IV.1431.{highest + 1}.2026", created.CaseNumber);
    }
}
