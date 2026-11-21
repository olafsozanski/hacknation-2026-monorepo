using System.Net.Http.Json;
using System.Text.Json;
using ClerkAssistant.Api.Features.Intake;
using ClerkAssistant.Api.Features.Search;
using ClerkAssistant.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClerkAssistant.Api.Tests.Ai;

public class RetrievalRegressionTests(DemoAiFactory factory) : IClassFixture<DemoAiFactory>
{
    private const double HighSimilarity = 0.66;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Request_about_pupils_finds_the_archived_pupils_case_from_the_previous_year()
    {
        var hits = await IntakeSimilarCasesAsync(await SampleAsync("BKS.IV.1431.599.2026", "requestText"));

        Assert.Contains("BKS.IV.1431.307.2025", hits.Take(3).Select(h => h.CaseNumber));
        Assert.All(hits.Where(h => h.CaseNumber is not ("BKS.IV.1431.599.2026" or "BKS.IV.1431.307.2025")),
            h => Assert.True(h.Score < HighSimilarity, $"{h.CaseNumber} scored {h.Score}"));
        Assert.True(hits.Single(h => h.CaseNumber == "BKS.IV.1431.307.2025").Score > hits.Skip(2).Max(h => h.Score));
    }

    [Fact]
    public async Task Subject_about_stray_animals_finds_the_previous_stray_animals_case()
    {
        var hits = await IntakeSimilarCasesAsync(await SampleAsync("BKS.IV.1431.543.2026", "subject"));

        Assert.Contains("BKS.IV.1431.293.2025", hits.Take(3).Select(h => h.CaseNumber));
    }

    [Fact]
    public async Task Similar_cases_of_the_astoria_documentation_request_include_the_astoria_case()
    {
        var id = await factory.QueryAsync(db => db.Requests
            .Where(r => r.CaseNumber == "BKS.IV.1431.509.2026")
            .Select(r => r.Id)
            .SingleAsync(Ct));
        var client = await factory.ClientAsync();

        var hits = await client.GetFromJsonAsync<SearchHit[]>($"/api/requests/{id}/similar", ApiClient.Json, Ct);

        Assert.Contains("BKS.IV.1431.590.2026", hits!.Take(5).Select(h => h.CaseNumber));
    }

    [Fact]
    public async Task Unrelated_text_does_not_produce_high_similarity()
    {
        var hits = await IntakeSimilarCasesAsync(
            "Wnoszę o udostępnienie informacji publicznej dotyczącej liczby hulajnóg elektrycznych zarejestrowanych w gminie.");

        Assert.All(hits, h => Assert.True(h.Score < HighSimilarity, $"{h.CaseNumber} scored {h.Score}"));
    }

    private async Task<SearchHit[]> IntakeSimilarCasesAsync(string text)
    {
        var client = await factory.ClientAsync();
        var response = await client.PostAsJsonAsync("/api/intake/text", new IntakeTextRequest(text), ApiClient.Json, Ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IntakeResult>(ApiClient.Json, Ct))!.SimilarCases;
    }

    private async Task<string> SampleAsync(string caseNumber, string property)
    {
        var root = factory.Services.GetRequiredService<IWebHostEnvironment>().ContentRootPath;
        await using var stream = File.OpenRead(Path.Combine(root, "Seed", "sample-cases.json"));
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: Ct);
        return json.RootElement.GetProperty("cases").EnumerateArray()
            .Single(c => c.GetProperty("caseNumber").GetString() == caseNumber)
            .GetProperty(property)
            .GetString()!;
    }
}
