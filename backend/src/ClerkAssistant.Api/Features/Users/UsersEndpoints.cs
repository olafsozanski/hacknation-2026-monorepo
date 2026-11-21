using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Features.Auth;
using ClerkAssistant.Api.Infrastructure.Common;
using ClerkAssistant.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ClerkAssistant.Api.Features.Users;

public static class UsersEndpoints
{
    public static void MapUsersEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/users", ListActiveUsers);

        var admin = api.MapGroup("/admin/users").RequireAuthorization(Roles.Admin);
        admin.MapGet("/", ListAllUsers);
        admin.MapPost("/", CreateUser);
        admin.MapPut("/{id:guid}", UpdateUser);
        admin.MapPost("/{id:guid}/password", ResetPassword);
    }

    private static async Task<List<UserSummary>> ListActiveUsers(AppDbContext db, CancellationToken ct) =>
        await db.Users
            .Where(u => u.IsActive)
            .OrderBy(u => u.FullName)
            .Select(u => new UserSummary(u.Id, u.FullName, u.Initials))
            .ToListAsync(ct);

    private static async Task<List<UserListItem>> ListAllUsers(
        AppDbContext db, UserManager<AppUser> users, AppClock clock, CancellationToken ct)
    {
        var admins = (await users.GetUsersInRoleAsync(Roles.Admin)).Select(u => u.Id).ToHashSet();
        var all = await db.Users.OrderBy(u => u.FullName).ToListAsync(ct);
        return all.Select(u => new UserListItem(
                u.Id,
                u.UserName!,
                u.FullName,
                u.Initials,
                admins.Contains(u.Id),
                u.IsActive,
                u.LockoutEnd > clock.Now))
            .ToList();
    }

    private static async Task<Results<Created<UserListItem>, ValidationProblem>> CreateUser(
        CreateUserRequest request, UserManager<AppUser> users, AuditLog audit, AppDbContext db, AppClock clock)
    {
        var user = new AppUser
        {
            UserName = request.UserName.Trim(),
            FullName = request.FullName.Trim(),
            Initials = request.Initials.Trim().ToUpperInvariant(),
            CreatedAt = clock.Now,
        };
        var result = await users.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return TypedResults.ValidationProblem(IdentityErrors.ToValidationErrors(result, "userName"));
        }

        await users.AddToRoleAsync(user, Roles.Clerk);
        if (request.IsAdmin)
        {
            await users.AddToRoleAsync(user, Roles.Admin);
        }

        audit.Record("user.created", nameof(AppUser), user.Id, user.UserName);
        await db.SaveChangesAsync();
        return TypedResults.Created($"/api/admin/users/{user.Id}",
            new UserListItem(user.Id, user.UserName, user.FullName, user.Initials, request.IsAdmin, true, false));
    }

    private static async Task<Results<NoContent, NotFound, ValidationProblem>> UpdateUser(
        Guid id, UpdateUserRequest request, UserManager<AppUser> users, CurrentUser current, AuditLog audit, AppDbContext db)
    {
        var user = await users.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return TypedResults.NotFound();
        }

        if (id == current.Id && (!request.IsActive || !request.IsAdmin))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["isAdmin"] = ["Nie możesz odebrać sobie uprawnień administratora ani dezaktywować własnego konta."],
            });
        }

        var deactivated = user.IsActive && !request.IsActive;
        user.FullName = request.FullName.Trim();
        user.Initials = request.Initials.Trim().ToUpperInvariant();
        user.IsActive = request.IsActive;
        await users.UpdateAsync(user);

        var isAdmin = await users.IsInRoleAsync(user, Roles.Admin);
        if (request.IsAdmin && !isAdmin)
        {
            await users.AddToRoleAsync(user, Roles.Admin);
        }
        else if (!request.IsAdmin && isAdmin)
        {
            await users.RemoveFromRoleAsync(user, Roles.Admin);
        }

        if (deactivated || request.IsAdmin != isAdmin)
        {
            await users.UpdateSecurityStampAsync(user);
        }

        audit.Record(deactivated ? "user.deactivated" : "user.updated", nameof(AppUser), user.Id, user.UserName);
        await db.SaveChangesAsync();
        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, NotFound, ValidationProblem>> ResetPassword(
        Guid id, ResetPasswordRequest request, UserManager<AppUser> users, AuditLog audit, AppDbContext db)
    {
        var user = await users.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return TypedResults.NotFound();
        }

        var token = await users.GeneratePasswordResetTokenAsync(user);
        var result = await users.ResetPasswordAsync(user, token, request.NewPassword);
        if (!result.Succeeded)
        {
            return TypedResults.ValidationProblem(IdentityErrors.ToValidationErrors(result, "newPassword"));
        }

        await users.SetLockoutEndDateAsync(user, null);
        await users.ResetAccessFailedCountAsync(user);
        audit.Record("user.password-reset", nameof(AppUser), user.Id, user.UserName);
        await db.SaveChangesAsync();
        return TypedResults.NoContent();
    }
}
