using System.Security.Claims;
using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Infrastructure.Common;
using ClerkAssistant.Api.Infrastructure.Security;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

namespace ClerkAssistant.Api.Features.Auth;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/auth");
        group.MapGet("/session", GetSession).AllowAnonymous();
        group.MapPost("/login", Login).AllowAnonymous().RequireRateLimiting(SecurityExtensions.LoginRateLimit);
        group.MapPost("/logout", Logout).AllowAnonymous();
        group.MapPost("/change-password", ChangePassword);
    }

    private static async Task<SessionResponse> GetSession(
        HttpContext http, UserManager<AppUser> users, IAntiforgery antiforgery)
    {
        var user = http.User.Identity?.IsAuthenticated == true ? await users.GetUserAsync(http.User) : null;
        return new SessionResponse(
            user is { IsActive: true } ? await ToSessionUser(user, users) : null,
            IssueToken(http, antiforgery));
    }

    private static async Task<Results<Ok<SessionResponse>, ProblemHttpResult>> Login(
        LoginRequest request,
        HttpContext http,
        UserManager<AppUser> users,
        SignInManager<AppUser> signIn,
        IAntiforgery antiforgery,
        ILogger<LoginRequest> logger)
    {
        const string invalidCredentials = "Nieprawidłowa nazwa użytkownika lub hasło.";
        var user = await users.FindByNameAsync(request.UserName.Trim());
        if (user is null || !user.IsActive)
        {
            return TypedResults.Problem(invalidCredentials, statusCode: StatusCodes.Status401Unauthorized);
        }

        var result = await signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (result.IsLockedOut)
        {
            logger.LogWarning("Account {UserId} locked out after failed sign-in attempts", user.Id);
            return TypedResults.Problem(
                "Konto zostało czasowo zablokowane po kilku nieudanych próbach logowania. Spróbuj ponownie za 15 minut.",
                statusCode: StatusCodes.Status423Locked);
        }

        if (!result.Succeeded)
        {
            return TypedResults.Problem(invalidCredentials, statusCode: StatusCodes.Status401Unauthorized);
        }

        await signIn.SignInAsync(user, isPersistent: false);
        var principal = await signIn.CreateUserPrincipalAsync(user);
        http.User = principal;
        return TypedResults.Ok(new SessionResponse(await ToSessionUser(user, users), IssueToken(http, antiforgery)));
    }

    private static async Task<SessionResponse> Logout(
        HttpContext http, SignInManager<AppUser> signIn, IAntiforgery antiforgery)
    {
        await signIn.SignOutAsync();
        http.User = new ClaimsPrincipal(new ClaimsIdentity());
        return new SessionResponse(null, IssueToken(http, antiforgery));
    }

    private static async Task<Results<NoContent, ValidationProblem>> ChangePassword(
        ChangePasswordRequest request, CurrentUser current, UserManager<AppUser> users, SignInManager<AppUser> signIn)
    {
        var user = await users.FindByIdAsync(current.Id.ToString());
        if (user is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["currentPassword"] = ["Nie znaleziono użytkownika."] });
        }

        var result = await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            return TypedResults.ValidationProblem(IdentityErrors.ToValidationErrors(result, "newPassword"));
        }

        await signIn.RefreshSignInAsync(user);
        return TypedResults.NoContent();
    }

    private static async Task<SessionUser> ToSessionUser(AppUser user, UserManager<AppUser> users) =>
        new(user.Id, user.UserName!, user.FullName, user.Initials, await users.IsInRoleAsync(user, Roles.Admin));

    private static string IssueToken(HttpContext http, IAntiforgery antiforgery) =>
        antiforgery.GetAndStoreTokens(http).RequestToken!;
}
