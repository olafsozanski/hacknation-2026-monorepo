using System.Security.Claims;
using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Infrastructure.Common;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;

namespace ClerkAssistant.Api.Features.Auth;

public sealed class AuthService(
    IHttpContextAccessor accessor,
    UserManager<AppUser> users,
    SignInManager<AppUser> signIn,
    IAntiforgery antiforgery,
    CurrentUser current,
    ILogger<AuthService> logger)
{
    private const string InvalidCredentials = "Nieprawidłowa nazwa użytkownika lub hasło.";

    private HttpContext Http => accessor.HttpContext ?? throw new InvalidOperationException("No HTTP context.");

    public async Task<SessionResponse> GetSessionAsync()
    {
        var user = Http.User.Identity?.IsAuthenticated == true ? await users.GetUserAsync(Http.User) : null;
        return new SessionResponse(user is { IsActive: true } ? await ToSessionUserAsync(user) : null, IssueToken());
    }

    public async Task<SessionResponse> LoginAsync(LoginRequest request)
    {
        var user = await users.FindByNameAsync(request.UserName.Trim());
        if (user is null || !user.IsActive)
        {
            throw new AuthenticationFailedException(InvalidCredentials);
        }

        var result = await signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (result.IsLockedOut)
        {
            logger.LogWarning("Account {UserId} locked out after failed sign-in attempts", user.Id);
            throw new AuthenticationFailedException(
                "Konto zostało czasowo zablokowane po kilku nieudanych próbach logowania. Spróbuj ponownie za 15 minut.",
                lockedOut: true);
        }

        if (!result.Succeeded)
        {
            throw new AuthenticationFailedException(InvalidCredentials);
        }

        await signIn.SignInAsync(user, isPersistent: false);
        Http.User = await signIn.CreateUserPrincipalAsync(user);
        return new SessionResponse(await ToSessionUserAsync(user), IssueToken());
    }

    public async Task<SessionResponse> LogoutAsync()
    {
        await signIn.SignOutAsync();
        Http.User = new ClaimsPrincipal(new ClaimsIdentity());
        return new SessionResponse(null, IssueToken());
    }

    public async Task ChangePasswordAsync(ChangePasswordRequest request)
    {
        var user = await users.FindByIdAsync(current.Id.ToString())
            ?? throw ValidationException.For("currentPassword", "Nie znaleziono użytkownika.");
        var result = await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            throw new ValidationException(IdentityErrors.ToValidationErrors(result, "newPassword"));
        }

        await signIn.RefreshSignInAsync(user);
    }

    private async Task<SessionUser> ToSessionUserAsync(AppUser user) =>
        user.ToSessionUser(await users.IsInRoleAsync(user, Roles.Admin));

    private string IssueToken() => antiforgery.GetAndStoreTokens(Http).RequestToken!;
}
