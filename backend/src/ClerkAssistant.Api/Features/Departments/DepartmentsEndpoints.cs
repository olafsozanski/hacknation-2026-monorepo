using System.Globalization;
using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Infrastructure.Common;
using ClerkAssistant.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace ClerkAssistant.Api.Features.Departments;

public static class DepartmentsEndpoints
{
    private static readonly StringComparer PolishOrder = StringComparer.Create(CultureInfo.GetCultureInfo("pl-PL"), ignoreCase: true);

    public static IServiceCollection AddDepartmentsFeature(this IServiceCollection services) => services;

    public static void MapDepartmentsEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/departments", List);
        api.MapPut("/admin/departments/{id:guid}", Update).RequireAuthorization(Roles.Admin);
    }

    private static async Task<DepartmentDto[]> List(bool? includeInactive, AppDbContext db, CancellationToken ct)
    {
        var departments = await db.Departments
            .AsNoTracking()
            .Where(d => includeInactive == true || d.IsActive)
            .ToListAsync(ct);
        return departments
            .OrderBy(d => d.Kind == DepartmentKind.Department ? 0 : 1)
            .ThenBy(d => d.Name, PolishOrder)
            .Select(d => new DepartmentDto(d.Id, d.Symbol, d.Name, d.Kind, d.Scope, d.Email, d.IsActive))
            .ToArray();
    }

    private static async Task<Results<NoContent, NotFound>> Update(
        Guid id, UpdateDepartmentRequest body, AppDbContext db, AuditLog audit, CancellationToken ct)
    {
        var department = await db.Departments.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (department is null)
        {
            return TypedResults.NotFound();
        }

        department.Name = body.Name.Trim();
        department.Scope = body.Scope?.Trim() ?? "";
        department.Email = string.IsNullOrWhiteSpace(body.Email) ? null : body.Email.Trim();
        department.IsActive = body.IsActive;
        audit.Record("department.updated", nameof(Department), id, department.Symbol);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }
}
