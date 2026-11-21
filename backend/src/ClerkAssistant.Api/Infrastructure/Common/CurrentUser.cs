using System.Security.Claims;

namespace ClerkAssistant.Api.Infrastructure.Common;

public sealed class CurrentUser(IHttpContextAccessor accessor)
{
    private ClaimsPrincipal Principal =>
        accessor.HttpContext?.User ?? throw new InvalidOperationException("No HTTP context.");

    public Guid Id => Guid.Parse(Principal.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("User is not authenticated."));

    public string UserName => Principal.Identity?.Name ?? "unknown";

    public bool IsInRole(string role) => Principal.IsInRole(role);
}
