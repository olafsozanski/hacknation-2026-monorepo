using ClerkAssistant.Api.Infrastructure.Security;

namespace ClerkAssistant.Api.Features.Auth;

public static class AuthEndpoints
{
    public static IServiceCollection AddAuthFeature(this IServiceCollection services) =>
        services.AddScoped<AuthService>();

    public static void MapAuthEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/auth");
        group.MapGet("/session", (AuthService auth) => auth.GetSessionAsync()).AllowAnonymous();
        group.MapPost("/login", (LoginRequest body, AuthService auth) => auth.LoginAsync(body))
            .AllowAnonymous()
            .RequireRateLimiting(SecurityExtensions.LoginRateLimit);
        group.MapPost("/logout", (AuthService auth) => auth.LogoutAsync()).AllowAnonymous();
        group.MapPost("/change-password", async (ChangePasswordRequest body, AuthService auth) =>
        {
            await auth.ChangePasswordAsync(body);
            return TypedResults.NoContent();
        });
    }
}
