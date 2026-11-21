using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Infrastructure.Persistence;

namespace ClerkAssistant.Api.Infrastructure.Common;

public sealed class AuditLog(AppDbContext db, CurrentUser user, AppClock clock)
{
    public void Record(string action, string entityType, Guid? entityId, string? details = null) =>
        db.AuditEntries.Add(new AuditEntry
        {
            At = clock.Now,
            UserId = user.Id,
            UserName = user.UserName,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = details,
        });
}
