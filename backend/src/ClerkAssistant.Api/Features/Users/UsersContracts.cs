using System.ComponentModel.DataAnnotations;

namespace ClerkAssistant.Api.Features.Users;

public record UserListItem(
    Guid Id,
    string UserName,
    string FullName,
    string Initials,
    bool IsAdmin,
    bool IsActive,
    bool IsLockedOut);

public record CreateUserRequest(
    [property: Required, MaxLength(64), RegularExpression(@"^[a-zA-Z0-9._@-]+$")] string UserName,
    [property: Required, MaxLength(200)] string FullName,
    [property: Required, MaxLength(8)] string Initials,
    [property: Required, MinLength(12), MaxLength(256)] string Password,
    bool IsAdmin);

public record UpdateUserRequest(
    [property: Required, MaxLength(200)] string FullName,
    [property: Required, MaxLength(8)] string Initials,
    bool IsAdmin,
    bool IsActive);

public record ResetPasswordRequest(
    [property: Required, MinLength(12), MaxLength(256)] string NewPassword);
