using System.Security.Claims;
using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Infrastructure.Common;
using ClerkAssistant.Api.Infrastructure.Persistence;
using ClerkAssistant.Api.Infrastructure.Security;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;

namespace ClerkAssistant.Api.Features.Auth;

public sealed class AuthService(
    IHttpContextAccessor accessor,
    UserManager<AppUser> users,
    SignInManager<AppUser> signIn,
    IAntiforgery antiforgery,
    CurrentUser current,
    AuditLog audit,
    AppDbContext db,
    AppClock clock,
    ILogger<AuthService> logger)
{
    private const string InvalidCredentials = "Nieprawidłowa nazwa użytkownika lub hasło.";
    private const string AuthEntity = "Authentication";

    private static readonly AppUser TimingDummy = new() { FullName = "", Initials = "" };
    private static readonly Lazy<string> TimingDummyHash = new(() => new PasswordHasher<AppUser>().HashPassword(TimingDummy, Guid.NewGuid().ToString()));

    private HttpContext Http => accessor.HttpContext ?? throw new InvalidOperationException("No HTTP context.");

    public async Task<SessionResponse> GetSessionAsync()
    {
        var user = Http.User.Identity?.IsAuthenticated == true ? await users.GetUserAsync(Http.User) : null;
        var sessionUser = user is { IsActive: true } ? await ToSessionUserAsync(user) : null;
        return sessionUser.ToSessionResponse(IssueToken());
    }

    public async Task<SessionResponse> LoginAsync(LoginRequest request)
    {
        var userName = request.UserName.Trim();
        var user = await users.FindByNameAsync(userName);
        if (user is null || !user.IsActive)
        {
            users.PasswordHasher.VerifyHashedPassword(TimingDummy, TimingDummyHash.Value, request.Password);
            await RecordAsync(user?.Id, userName, "auth.login-failed", user is null ? "nieznany użytkownik" : "konto nieaktywne");
            throw new AuthenticationFailedException(InvalidCredentials);
        }

        var result = await signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (result.IsLockedOut)
        {
            logger.LogWarning("Account {UserId} locked out after failed sign-in attempts", user.Id);
            await RecordAsync(user.Id, userName, "auth.locked-out", null);
            throw new AuthenticationFailedException(
                "Konto zostało czasowo zablokowane po kilku nieudanych próbach logowania. Spróbuj ponownie za 15 minut.",
                lockedOut: true);
        }

        if (!result.Succeeded)
        {
            await RecordAsync(user.Id, userName, "auth.login-failed", "błędne hasło");
            throw new AuthenticationFailedException(InvalidCredentials);
        }

        await signIn.SignInWithClaimsAsync(user, isPersistent: false, [SessionLifetime.CreateClaim(clock.Now)]);
        Http.User = await signIn.CreateUserPrincipalAsync(user);
        await RecordAsync(user.Id, userName, "auth.login", null);
        return (await ToSessionUserAsync(user)).ToSessionResponse(IssueToken());
    }

    public async Task<SessionResponse> LogoutAsync()
    {
        if (Http.User.Identity?.IsAuthenticated == true && await users.GetUserAsync(Http.User) is { } user)
        {
            await users.UpdateSecurityStampAsync(user);
            await RecordAsync(user.Id, user.UserName ?? "", "auth.logout", null);
        }

        await signIn.SignOutAsync();
        Http.User = new ClaimsPrincipal(new ClaimsIdentity());
        return ((SessionUser?)null).ToSessionResponse(IssueToken());
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

        var authTime = SessionLifetime.Find(Http.User) ?? SessionLifetime.CreateClaim(clock.Now);
        await signIn.SignInWithClaimsAsync(user, isPersistent: false, [new Claim(authTime.Type, authTime.Value, authTime.ValueType)]);
        await RecordAsync(user.Id, user.UserName ?? "", "auth.password-changed", null);
    }

    private async Task RecordAsync(Guid? userId, string userName, string action, string? details)
    {
        audit.RecordFor(userId, userName, action, AuthEntity, userId, details);
        await db.SaveChangesAsync();
    }

    private async Task<SessionUser> ToSessionUserAsync(AppUser user) =>
        user.ToSessionUser(await users.IsInRoleAsync(user, Roles.Admin));

    private string IssueToken() => antiforgery.GetAndStoreTokens(Http).RequestToken!;
}
