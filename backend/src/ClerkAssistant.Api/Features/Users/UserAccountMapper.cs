using ClerkAssistant.Api.Domain;
using Riok.Mapperly.Abstractions;

namespace ClerkAssistant.Api.Features.Users;

[Mapper]
public static partial class UserAccountMapper
{
    [MapProperty(nameof(AppUser.UserName), nameof(UserListItem.UserName), Use = nameof(RequiredUserName))]
    public static partial UserListItem ToListItem(this AppUser user, bool isAdmin, bool isLockedOut);

    private static string RequiredUserName(string? userName) => userName ?? "";
}
