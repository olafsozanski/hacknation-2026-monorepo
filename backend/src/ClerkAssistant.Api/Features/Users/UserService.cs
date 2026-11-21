using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Features.Auth;
using ClerkAssistant.Api.Infrastructure.Common;
using ClerkAssistant.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ClerkAssistant.Api.Features.Users;

public sealed class UserService(
    AppDbContext db,
    UserManager<AppUser> users,
    CurrentUser current,
    AuditLog audit,
    AppClock clock)
{
    public async Task<UserSummary[]> ListActiveAsync(CancellationToken ct)
    {
        var active = await db.Users.AsNoTracking().Where(u => u.IsActive).OrderBy(u => u.FullName).ToListAsync(ct);
        return active.Select(u => u.ToSummary()).ToArray();
    }

    public async Task<UserListItem[]> ListAllAsync(CancellationToken ct)
    {
        var admins = (await users.GetUsersInRoleAsync(Roles.Admin)).Select(u => u.Id).ToHashSet();
        var all = await db.Users.AsNoTracking().OrderBy(u => u.FullName).ToListAsync(ct);
        return all.Select(u => u.ToListItem(admins.Contains(u.Id), u.LockoutEnd > clock.Now)).ToArray();
    }

    public async Task<UserListItem> CreateAsync(CreateUserRequest request, CancellationToken ct)
    {
        var user = new AppUser
        {
            UserName = request.UserName.Trim(),
            FullName = request.FullName.Trim(),
            Initials = request.Initials.Trim().ToUpperInvariant(),
            CreatedAt = clock.Now,
        };
        EnsureSucceeded(await users.CreateAsync(user, request.Password), "userName");
        await users.AddToRoleAsync(user, Roles.Clerk);
        if (request.IsAdmin)
        {
            await users.AddToRoleAsync(user, Roles.Admin);
        }

        audit.Record("user.created", nameof(AppUser), user.Id, user.UserName);
        await db.SaveChangesAsync(ct);
        return user.ToListItem(request.IsAdmin, isLockedOut: false);
    }

    public async Task UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(id.ToString()) ?? throw new NotFoundException("Nie znaleziono użytkownika.");
        if (id == current.Id && (!request.IsActive || !request.IsAdmin))
        {
            throw ValidationException.For("isAdmin", "Nie możesz odebrać sobie uprawnień administratora ani dezaktywować własnego konta.");
        }

        var deactivated = user.IsActive && !request.IsActive;
        var reactivated = !user.IsActive && request.IsActive;
        user.FullName = request.FullName.Trim();
        user.Initials = request.Initials.Trim().ToUpperInvariant();
        user.IsActive = request.IsActive;
        await users.UpdateAsync(user);

        var wasAdmin = await users.IsInRoleAsync(user, Roles.Admin);
        if (request.IsAdmin && !wasAdmin)
        {
            await users.AddToRoleAsync(user, Roles.Admin);
        }
        else if (!request.IsAdmin && wasAdmin)
        {
            await users.RemoveFromRoleAsync(user, Roles.Admin);
        }

        if (deactivated || request.IsAdmin != wasAdmin)
        {
            await users.UpdateSecurityStampAsync(user);
        }

        var changes = new (bool Applies, string Text)[]
            {
                (request.IsAdmin && !wasAdmin, "nadano uprawnienia administratora"),
                (!request.IsAdmin && wasAdmin, "odebrano uprawnienia administratora"),
                (deactivated, "dezaktywowano konto"),
                (reactivated, "aktywowano konto"),
            }
            .Where(c => c.Applies)
            .Select(c => c.Text)
            .ToList();
        audit.Record(
            deactivated ? "user.deactivated" : request.IsAdmin != wasAdmin ? "user.role-changed" : "user.updated",
            nameof(AppUser),
            user.Id,
            changes.Count > 0 ? $"{user.UserName}: {string.Join(", ", changes)}" : user.UserName);
        await db.SaveChangesAsync(ct);
    }

    public async Task ResetPasswordAsync(Guid id, ResetPasswordRequest request, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(id.ToString()) ?? throw new NotFoundException("Nie znaleziono użytkownika.");
        var token = await users.GeneratePasswordResetTokenAsync(user);
        EnsureSucceeded(await users.ResetPasswordAsync(user, token, request.NewPassword), "newPassword");
        await users.SetLockoutEndDateAsync(user, null);
        await users.ResetAccessFailedCountAsync(user);
        await users.UpdateSecurityStampAsync(user);
        audit.Record("user.password-reset", nameof(AppUser), user.Id, user.UserName);
        await db.SaveChangesAsync(ct);
    }

    private static void EnsureSucceeded(IdentityResult result, string field)
    {
        if (!result.Succeeded)
        {
            throw new ValidationException(IdentityErrors.ToValidationErrors(result, field));
        }
    }
}
