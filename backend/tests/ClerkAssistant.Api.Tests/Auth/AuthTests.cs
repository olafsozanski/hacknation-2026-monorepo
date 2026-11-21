using System.Net;
using System.Net.Http.Json;
using ClerkAssistant.Api.Features.Auth;
using ClerkAssistant.Api.Tests.Infrastructure;

namespace ClerkAssistant.Api.Tests.Auth;

public class AuthTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Api_requires_authentication()
    {
        var client = factory.CreateAnonymousClient();

        var response = await client.GetAsync("/api/users", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_without_csrf_token_is_rejected()
    {
        var client = factory.CreateAnonymousClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(ApiFactory.AdminUserName, ApiFactory.AdminPassword),
            ApiClient.Json,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_with_wrong_password_returns_unauthorized()
    {
        var client = await factory.CreateClientWithCsrfAsync();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(ApiFactory.AdminUserName, "WrongPassword123"),
            ApiClient.Json,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Authenticated_session_returns_current_user()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var session = await client.GetFromJsonAsync<SessionResponse>(
            "/api/auth/session", ApiClient.Json, TestContext.Current.CancellationToken);

        Assert.NotNull(session?.User);
        Assert.True(session.User.IsAdmin);
    }
}
