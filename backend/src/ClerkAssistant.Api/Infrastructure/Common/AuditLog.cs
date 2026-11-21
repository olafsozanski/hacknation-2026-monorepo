using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Infrastructure.Persistence;

namespace ClerkAssistant.Api.Infrastructure.Common;

public sealed class AuditLog(AppDbContext db, CurrentUser user, AppClock clock)
{
    private const int MaxUserNameLength = 256;

    public void Record(string action, string entityType, Guid? entityId, string? details = null) =>
        RecordFor(user.Id, user.UserName, action, entityType, entityId, details);

    public void RecordFor(Guid? userId, string userName, string action, string entityType, Guid? entityId, string? details = null) =>
        db.AuditEntries.Add(new AuditEntry
        {
            At = clock.Now,
            UserId = userId,
            UserName = userName.Length > MaxUserNameLength ? userName[..MaxUserNameLength] : userName,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = details,
        });
}
