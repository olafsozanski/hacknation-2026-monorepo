using ClerkAssistant.Api.Domain;

namespace ClerkAssistant.Api.Features.Departments;

public static class DepartmentsEndpoints
{
    public static IServiceCollection AddDepartmentsFeature(this IServiceCollection services) =>
        services.AddScoped<DepartmentService>();

    public static void MapDepartmentsEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/departments", (bool? includeInactive, DepartmentService departments, CancellationToken ct) =>
            departments.ListAsync(includeInactive == true, ct));

        api.MapPut("/admin/departments/{id:guid}", async (Guid id, UpdateDepartmentRequest body, DepartmentService departments, CancellationToken ct) =>
            {
                await departments.UpdateAsync(id, body, ct);
                return TypedResults.NoContent();
            })
            .RequireAuthorization(Roles.Admin);
    }
}
