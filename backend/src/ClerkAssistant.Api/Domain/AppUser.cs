using Microsoft.AspNetCore.Identity;

namespace ClerkAssistant.Api.Domain;

public class AppUser : IdentityUser<Guid>
{
    public required string FullName { get; set; }
    public required string Initials { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
}

public static class Roles
{
    public const string Clerk = "Clerk";
    public const string Admin = "Admin";
}
