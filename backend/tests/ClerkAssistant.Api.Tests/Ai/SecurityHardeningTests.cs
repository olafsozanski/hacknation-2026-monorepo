using System.Net;
using System.Net.Http.Json;
using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Features.Anonymization;
using ClerkAssistant.Api.Features.Drafts;
using ClerkAssistant.Api.Features.Intake;
using ClerkAssistant.Api.Features.Knowledge;
using ClerkAssistant.Api.Features.Users;
using ClerkAssistant.Api.Infrastructure.Security;
using ClerkAssistant.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClerkAssistant.Api.Tests.Ai;

public class SecurityHardeningTests(AiApiFactory factory) : IClassFixture<AiApiFactory>
{
    private const string ConcurrencyTitle = "Dane zostały w międzyczasie zmienione przez innego użytkownika.";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Regenerating_a_draft_requires_its_current_version()
    {
        var id = await factory.AddRequestAsync(new NewRequest("Koszt festynu", "Ile kosztował festyn?", new DateOnly(2026, 11, 16)));
        await factory.AddDepartmentReplyAsync(id, "BKB", "Festyn kosztował 100 zł.");
        var client = await factory.ClientAsync();

        var first = await client.PostAsync($"/api/requests/{id}/draft/generate", null, Ct);
        first.EnsureSuccessStatusCode();
        var version = (await first.Content.ReadFromJsonAsync<GenerateDraftResult>(ApiClient.Json, Ct))!.Draft.Version;
        await factory.AddDepartmentReplyAsync(id, "WIM", "Inwestycja nie dotyczyła festynu.");

        var withoutVersion = await client.PostAsync($"/api/requests/{id}/draft/generate", null, Ct);
        var stale = await client.PostAsync($"/api/requests/{id}/draft/generate?version={version + 1}", null, Ct);
        var current = await client.PostAsync($"/api/requests/{id}/draft/generate?version={version}", null, Ct);

        Assert.Equal(HttpStatusCode.Conflict, withoutVersion.StatusCode);
        Assert.Contains(ConcurrencyTitle, await withoutVersion.Content.ReadAsStringAsync(Ct));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        current.EnsureSuccessStatusCode();
        Assert.NotEqual(version, (await current.Content.ReadFromJsonAsync<GenerateDraftResult>(ApiClient.Json, Ct))!.Draft.Version);
    }

    [Fact]
    public async Task Deleting_knowledge_requires_an_administrator()
    {
        var admin = await factory.ClientAsync();
        var upload = await admin.PostAsync("/api/knowledge", TestData.TextFile("zasady.txt", "Zasady udostępniania.", ("kind", "Guideline")), Ct);
        var document = (await upload.Content.ReadFromJsonAsync<KnowledgeDocumentDto>(ApiClient.Json, Ct))!;
        var created = await admin.PostAsJsonAsync("/api/admin/users",
            new CreateUserRequest("urzednik.wiedza", "Urzędnik Wiedzy", "UW", "Urzednik12345", false), ApiClient.Json, Ct);
        created.EnsureSuccessStatusCode();
        var clerk = await factory.CreateAuthenticatedClientAsync("urzednik.wiedza", "Urzednik12345");

        var byClerk = await clerk.DeleteAsync($"/api/knowledge/{document.Id}", Ct);
        var listedByClerk = await clerk.GetAsync("/api/knowledge", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, byClerk.StatusCode);
        Assert.Equal(HttpStatusCode.OK, listedByClerk.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/knowledge/{document.Id}", Ct)).StatusCode);
    }

    [Theory]
    [InlineData("POST", "/api/knowledge", SecurityExtensions.UploadRateLimit)]
    [InlineData("GET", "/api/search", SecurityExtensions.SearchRateLimit)]
    [InlineData("POST", "/api/intake", SecurityExtensions.AiRateLimit)]
    [InlineData("POST", "/api/anonymization/analyze", SecurityExtensions.AiRateLimit)]
    [InlineData("POST", "/api/requests/{id:guid}/draft/generate", SecurityExtensions.AiRateLimit)]
    public async Task Endpoints_use_the_expected_rate_limit_policy(string method, string route, string policy)
    {
        await factory.ClientAsync();
        var endpoint = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Single(e => e.RoutePattern.RawText?.TrimEnd('/') == route
                         && e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains(method) == true);

        Assert.Equal(policy, endpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName);
    }

    [Fact]
    public async Task Oversized_redaction_lists_are_rejected()
    {
        var id = await factory.AddRequestAsync(new NewRequest("Temat", "Treść", new DateOnly(2026, 11, 16)));
        var redactions = Enumerable.Range(0, RedactionSet.MaxRedactions + 1)
            .Select(i => new Redaction(i, 1, "a", RedactionCategory.Other, RedactionStatus.Approved, RedactionSource.Manual))
            .ToArray();
        var client = await factory.ClientAsync();

        var detect = await client.PostAsJsonAsync(
            $"/api/requests/{id}/draft/detect", new DetectRedactionsRequest("Treść", redactions), ApiClient.Json, Ct);
        var save = await client.PutAsJsonAsync(
            $"/api/requests/{id}/draft", new SaveDraftRequest("Treść", redactions, null), ApiClient.Json, Ct);
        var export = await client.PostAsJsonAsync(
            "/api/anonymization/export", new AnonymizationExportRequest("Treść", redactions, "txt", null), ApiClient.Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, detect.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, save.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, export.StatusCode);
    }

    [Fact]
    public async Task Hourly_cleanup_removes_stale_unattached_files_only()
    {
        var (stale, fresh) = await factory.QueryAsync(async db =>
        {
            StoredFile File(int ageHours) => new()
            {
                FileName = "plik.txt",
                ContentType = "text/plain",
                Content = [1],
                Sha256 = "x",
                CreatedAt = factory.Clock.GetUtcNow().AddHours(-ageHours),
            };
            var staleFile = File(30);
            var freshFile = File(2);
            db.Files.AddRange(staleFile, freshFile);
            await db.SaveChangesAsync(Ct);
            return (staleFile.Id, freshFile.Id);
        });
        await factory.ClientAsync();

        await factory.Services.GetRequiredService<UnattachedFileCleanup>().RunOnceAsync(Ct);

        Assert.False(await factory.QueryAsync(db => db.Files.AnyAsync(f => f.Id == stale, Ct)));
        Assert.True(await factory.QueryAsync(db => db.Files.AnyAsync(f => f.Id == fresh, Ct)));
    }
}
