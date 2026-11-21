using ClerkAssistant.Api.Domain;
using Riok.Mapperly.Abstractions;

namespace ClerkAssistant.Api.Features.Auth;

[Mapper]
public static partial class SessionMapper
{
    [MapProperty(nameof(AppUser.UserName), nameof(SessionUser.UserName), Use = nameof(RequiredUserName))]
    public static partial SessionUser ToSessionUser(this AppUser user, bool isAdmin);

    public static SessionResponse ToSessionResponse(this SessionUser? user, string csrfToken) => new(user, csrfToken);

    private static string RequiredUserName(string? userName) => userName ?? "";
}
