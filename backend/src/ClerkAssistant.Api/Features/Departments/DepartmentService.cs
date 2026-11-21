using System.Globalization;
using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Infrastructure.Common;
using ClerkAssistant.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClerkAssistant.Api.Features.Departments;

public sealed class DepartmentService(AppDbContext db, AuditLog audit)
{
    private static readonly StringComparer PolishOrder = StringComparer.Create(CultureInfo.GetCultureInfo("pl-PL"), ignoreCase: true);

    public async Task<DepartmentDto[]> ListAsync(bool includeInactive, CancellationToken ct)
    {
        var departments = await db.Departments
            .AsNoTracking()
            .Where(d => includeInactive || d.IsActive)
            .ToListAsync(ct);
        return departments
            .OrderBy(d => d.Kind == DepartmentKind.Department ? 0 : 1)
            .ThenBy(d => d.Name, PolishOrder)
            .Select(d => d.ToDto())
            .ToArray();
    }

    public async Task UpdateAsync(Guid id, UpdateDepartmentRequest body, CancellationToken ct)
    {
        var department = await db.Departments.FirstOrDefaultAsync(d => d.Id == id, ct)
            ?? throw new NotFoundException("Nie znaleziono wydziału.");

        department.Name = body.Name.Trim();
        department.Scope = body.Scope?.Trim() ?? "";
        department.Email = string.IsNullOrWhiteSpace(body.Email) ? null : body.Email.Trim();
        department.IsActive = body.IsActive;
        audit.Record("department.updated", nameof(Department), id, department.Symbol);
        await db.SaveChangesAsync(ct);
    }
}
