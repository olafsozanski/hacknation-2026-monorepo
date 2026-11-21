using System.Threading.RateLimiting;
using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;

namespace ClerkAssistant.Api.Infrastructure.Security;

public static class SecurityExtensions
{
    public const string LoginRateLimit = "login";
    public const string AiRateLimit = "ai";
    public const string AntiforgeryHeader = "X-XSRF-TOKEN";
    public const long MaxUploadBytes = 20 * 1024 * 1024;

    public static WebApplicationBuilder AddSecurity(this WebApplicationBuilder builder)
    {
        var services = builder.Services;
        var cookieSecurity = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;

        builder.WebHost.ConfigureKestrel(o =>
        {
            o.AddServerHeader = false;
            o.Limits.MaxRequestBodySize = MaxUploadBytes + 1024 * 1024;
        });
        services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = MaxUploadBytes);

        services.AddDataProtection()
            .SetApplicationName("ClerkAssistant")
            .PersistKeysToDbContext<AppDbContext>();

        services.AddIdentityCore<AppUser>(o =>
            {
                o.Password.RequiredLength = 12;
                o.Password.RequireDigit = true;
                o.Password.RequireLowercase = true;
                o.Password.RequireUppercase = true;
                o.Password.RequireNonAlphanumeric = false;
                o.Lockout.AllowedForNewUsers = true;
                o.Lockout.MaxFailedAccessAttempts = 5;
                o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                o.User.RequireUniqueEmail = false;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager();

        services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.FromMinutes(1));
        services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
        services.ConfigureApplicationCookie(o =>
        {
            o.Cookie.Name = "udip.session";
            o.Cookie.HttpOnly = true;
            o.Cookie.SameSite = SameSiteMode.Strict;
            o.Cookie.SecurePolicy = cookieSecurity;
            o.ExpireTimeSpan = TimeSpan.FromHours(8);
            o.SlidingExpiration = true;
            o.Events.OnRedirectToLogin = ctx =>
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            o.Events.OnRedirectToAccessDenied = ctx =>
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(Roles.Admin, p => p.RequireRole(Roles.Admin));

        services.AddAntiforgery(o =>
        {
            o.HeaderName = AntiforgeryHeader;
            o.Cookie.Name = "udip.af";
            o.Cookie.HttpOnly = true;
            o.Cookie.SameSite = SameSiteMode.Strict;
            o.Cookie.SecurePolicy = cookieSecurity;
        });

        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.AddPolicy(LoginRateLimit, ctx => RateLimitPartition.GetFixedWindowLimiter(
                ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(5) }));
            o.AddPolicy(AiRateLimit, ctx => RateLimitPartition.GetFixedWindowLimiter(
                ctx.User.Identity?.Name ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1) }));
        });

        return builder;
    }

    public static WebApplication UseSecurityHeaders(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
        }

        app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Cross-Origin-Opener-Policy"] = "same-origin";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
            headers["Content-Security-Policy"] =
                "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data: blob:; " +
                "font-src 'self'; connect-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                headers.CacheControl = "no-store";
            }

            await next();
        });

        return app;
    }
}
