using ClerkAssistant.Api.Domain;

namespace ClerkAssistant.Api.Infrastructure.Common;

public record UserSummary(Guid Id, string FullName, string Initials)
{
    public static UserSummary? From(AppUser? user) =>
        user is null ? null : new UserSummary(user.Id, user.FullName, user.Initials);
}
