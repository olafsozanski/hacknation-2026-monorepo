using ClerkAssistant.Api.Domain;

namespace ClerkAssistant.Api.Features.Users;

public static class UsersEndpoints
{
    public static IServiceCollection AddUsersFeature(this IServiceCollection services) =>
        services.AddScoped<UserService>();

    public static void MapUsersEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/users", (UserService users, CancellationToken ct) => users.ListActiveAsync(ct));

        var admin = api.MapGroup("/admin/users").RequireAuthorization(Roles.Admin);
        admin.MapGet("/", (UserService users, CancellationToken ct) => users.ListAllAsync(ct));
        admin.MapPost("/", async (CreateUserRequest body, UserService users, CancellationToken ct) =>
        {
            var created = await users.CreateAsync(body, ct);
            return TypedResults.Created($"/api/admin/users/{created.Id}", created);
        });
        admin.MapPut("/{id:guid}", async (Guid id, UpdateUserRequest body, UserService users, CancellationToken ct) =>
        {
            await users.UpdateAsync(id, body, ct);
            return TypedResults.NoContent();
        });
        admin.MapPost("/{id:guid}/password", async (Guid id, ResetPasswordRequest body, UserService users, CancellationToken ct) =>
        {
            await users.ResetPasswordAsync(id, body, ct);
            return TypedResults.NoContent();
        });
    }
}
