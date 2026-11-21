using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClerkAssistant.Api.Features.Auth;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ClerkAssistant.Api.Tests.Infrastructure;

public static class ApiClient
{
    public const string CsrfHeader = "X-XSRF-TOKEN";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static HttpClient CreateAnonymousClient(this ApiFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
        });

    public static async Task<HttpClient> CreateClientWithCsrfAsync(this ApiFactory factory)
    {
        var client = factory.CreateAnonymousClient();
        var session = await client.GetFromJsonAsync<SessionResponse>("/api/auth/session", Json);
        client.SetCsrfToken(session!.CsrfToken);
        return client;
    }

    public static async Task<HttpClient> CreateAuthenticatedClientAsync(
        this ApiFactory factory,
        string userName = ApiFactory.AdminUserName,
        string password = ApiFactory.AdminPassword)
    {
        var client = await factory.CreateClientWithCsrfAsync();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(userName, password), Json);
        response.EnsureSuccessStatusCode();
        var session = await response.Content.ReadFromJsonAsync<SessionResponse>(Json);
        client.SetCsrfToken(session!.CsrfToken);
        return client;
    }

    public static void SetCsrfToken(this HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Remove(CsrfHeader);
        client.DefaultRequestHeaders.Add(CsrfHeader, token);
    }
}
