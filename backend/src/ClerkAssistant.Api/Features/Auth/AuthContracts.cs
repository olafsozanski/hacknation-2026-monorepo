using System.ComponentModel.DataAnnotations;

namespace ClerkAssistant.Api.Features.Auth;

public record LoginRequest(
    [property: Required, MaxLength(256)] string UserName,
    [property: Required, MaxLength(256)] string Password);

public record ChangePasswordRequest(
    [property: Required, MaxLength(256)] string CurrentPassword,
    [property: Required, MinLength(12), MaxLength(256)] string NewPassword);

public record SessionUser(Guid Id, string UserName, string FullName, string Initials, bool IsAdmin);

public record SessionResponse(SessionUser? User, string CsrfToken);
