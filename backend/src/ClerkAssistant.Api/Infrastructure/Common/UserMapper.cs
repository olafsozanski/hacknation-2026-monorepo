using ClerkAssistant.Api.Domain;
using Riok.Mapperly.Abstractions;

namespace ClerkAssistant.Api.Infrastructure.Common;

[Mapper]
public static partial class UserMapper
{
    public static partial UserSummary ToSummary(this AppUser user);

    public static UserSummary? ToSummaryOrNull(this AppUser? user) => user?.ToSummary();
}
