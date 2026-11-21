using System.Globalization;
using System.Security.Claims;

namespace ClerkAssistant.Api.Infrastructure.Security;

public static class SessionLifetime
{
    public const string AuthTimeClaim = "auth_time";

    public static Claim CreateClaim(DateTimeOffset now) =>
        new(AuthTimeClaim, now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64);

    public static Claim? Find(ClaimsPrincipal? principal) => principal?.FindFirst(AuthTimeClaim);

    public static bool IsExpired(ClaimsPrincipal principal, DateTimeOffset now, TimeSpan maxAge)
    {
        var claim = Find(principal);
        if (claim is null || !long.TryParse(claim.Value, CultureInfo.InvariantCulture, out var seconds))
        {
            return true;
        }

        return now - DateTimeOffset.FromUnixTimeSeconds(seconds) > maxAge;
    }
}
