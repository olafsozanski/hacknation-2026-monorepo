using System.Text.Json;
using System.Text.Json.Serialization;
using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ClerkAssistant.Api.Infrastructure.Seeding;

public static class DatabaseInitializer
{
    private sealed record DepartmentSeed(string Symbol, string Name, DepartmentKind Kind, string Scope, string? Email);

    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<AppDbContext>();
        var options = services.GetRequiredService<IOptions<SeedOptions>>().Value;
        var logger = services.GetRequiredService<ILogger<AppDbContext>>();

        await db.Database.MigrateAsync();
        await SeedRolesAsync(services.GetRequiredService<RoleManager<IdentityRole<Guid>>>());
        await SeedDepartmentsAsync(db, app.Environment.ContentRootPath);
        await SeedAdminAsync(services.GetRequiredService<UserManager<AppUser>>(), options, logger);

        if (options.DemoData)
        {
            await services.GetRequiredService<DemoDataSeeder>().SeedAsync();
        }
    }

    private static async Task SeedRolesAsync(RoleManager<IdentityRole<Guid>> roles)
    {
        foreach (var role in new[] { Roles.Clerk, Roles.Admin })
        {
            if (!await roles.RoleExistsAsync(role))
            {
                await roles.CreateAsync(new IdentityRole<Guid>(role));
            }
        }
    }

    private static async Task SeedDepartmentsAsync(AppDbContext db, string contentRoot)
    {
        if (await db.Departments.AnyAsync())
        {
            return;
        }

        var json = await File.ReadAllTextAsync(Path.Combine(contentRoot, "Seed", "departments.json"));
        var seeds = JsonSerializer.Deserialize<List<DepartmentSeed>>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() },
        }) ?? [];

        db.Departments.AddRange(seeds.Select(s => new Department
        {
            Symbol = s.Symbol,
            Name = s.Name,
            Kind = s.Kind,
            Scope = s.Scope,
            Email = s.Email,
        }));
        await db.SaveChangesAsync();
    }

    private static async Task SeedAdminAsync(UserManager<AppUser> users, SeedOptions options, ILogger logger)
    {
        if (await users.Users.AnyAsync())
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(options.AdminPassword))
        {
            logger.LogWarning("No users exist and Seed:AdminPassword is not set. Set it to create the first administrator.");
            return;
        }

        var admin = new AppUser
        {
            UserName = options.AdminUserName,
            FullName = "Administrator",
            Initials = "ADM",
            CreatedAt = DateTimeOffset.UtcNow,
        };
        var result = await users.CreateAsync(admin, options.AdminPassword);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                "Could not create the administrator account: " + string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        await users.AddToRolesAsync(admin, [Roles.Clerk, Roles.Admin]);
    }
}
