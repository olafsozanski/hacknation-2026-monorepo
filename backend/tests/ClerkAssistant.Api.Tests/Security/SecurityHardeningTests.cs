using System.Net;
using System.Net.Http.Json;
using ClerkAssistant.Api.Features.Audit;
using ClerkAssistant.Api.Features.Auth;
using ClerkAssistant.Api.Features.Users;
using ClerkAssistant.Api.Infrastructure.Persistence;
using ClerkAssistant.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClerkAssistant.Api.Tests.Security;

public class SecurityHardeningTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Plain_http_is_rejected_when_https_is_required()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("http://localhost") });

        var response = await client.GetAsync("/api/auth/session", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Sign_in_events_are_audited_and_visible_to_admins_only()
    {
        var anonymous = await factory.CreateClientWithCsrfAsync();
        await anonymous.PostAsJsonAsync("/api/auth/login", new LoginRequest(ApiFactory.AdminUserName, "Wrong-Password-123"), ApiClient.Json, Ct);
        var admin = await factory.CreateAuthenticatedClientAsync();

        var entries = await admin.GetFromJsonAsync<AuditEntryDto[]>("/api/admin/audit?action=auth.", ApiClient.Json, Ct);

        Assert.Contains(entries!, e => e.Action == "auth.login-failed" && e.UserName == ApiFactory.AdminUserName);
        Assert.Contains(entries!, e => e.Action == "auth.login" && e.UserName == ApiFactory.AdminUserName);

        await admin.PostAsJsonAsync("/api/admin/users", new CreateUserRequest("urzednik.audyt", "Jan Audyt", "JA", "Clerk-Password-2026", false), ApiClient.Json, Ct);
        var clerk = await factory.CreateAuthenticatedClientAsync("urzednik.audyt", "Clerk-Password-2026");
        var forbidden = await clerk.GetAsync("/api/admin/audit", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task Audit_entries_cannot_be_modified_or_deleted()
    {
        await factory.CreateAuthenticatedClientAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await Assert.ThrowsAnyAsync<Exception>(() => db.AuditEntries.ExecuteUpdateAsync(s => s.SetProperty(e => e.Details, "zmienione"), Ct));
        await Assert.ThrowsAnyAsync<Exception>(() => db.AuditEntries.ExecuteDeleteAsync(Ct));
    }

    [Fact]
    public async Task Numeric_enum_values_are_rejected()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var body = new { subject = "Test", replyChannel = 0, receivedOn = "2026-11-17" };

        var response = await client.PostAsJsonAsync("/api/requests", body, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
