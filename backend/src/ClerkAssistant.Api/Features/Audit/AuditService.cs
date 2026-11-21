using ClerkAssistant.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClerkAssistant.Api.Features.Audit;

public sealed class AuditService(AppDbContext db)
{
    private const int MaxPageSize = 500;

    public async Task<AuditEntryDto[]> ListAsync(long? before, string? action, string? userName, int take, CancellationToken ct)
    {
        var query = db.AuditEntries.AsNoTracking();
        if (before is { } b)
        {
            query = query.Where(e => e.Id < b);
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            query = query.Where(e => e.Action.StartsWith(action.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(userName))
        {
            query = query.Where(e => e.UserName == userName.Trim());
        }

        return await query
            .OrderByDescending(e => e.Id)
            .Take(Math.Clamp(take, 1, MaxPageSize))
            .ProjectToDto()
            .ToArrayAsync(ct);
    }
}
