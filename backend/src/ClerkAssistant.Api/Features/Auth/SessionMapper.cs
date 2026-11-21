using ClerkAssistant.Api.Domain;
using Riok.Mapperly.Abstractions;

namespace ClerkAssistant.Api.Features.Auth;

[Mapper]
public static partial class SessionMapper
{
    [MapProperty(nameof(AppUser.UserName), nameof(SessionUser.UserName), Use = nameof(RequiredUserName))]
    public static partial SessionUser ToSessionUser(this AppUser user, bool isAdmin);

    private static string RequiredUserName(string? userName) => userName ?? "";
}
