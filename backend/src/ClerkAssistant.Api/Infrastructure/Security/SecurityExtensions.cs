using System.Net;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Threading.RateLimiting;
using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;

namespace ClerkAssistant.Api.Infrastructure.Security;

public static class SecurityExtensions
{
    public const string LoginRateLimit = "login";
    public const string AiRateLimit = "ai";
    public const string UploadRateLimit = "uploads";
    public const string SearchRateLimit = "search";
    public const string AntiforgeryHeader = "X-XSRF-TOKEN";
    public const long MaxUploadBytes = 20 * 1024 * 1024;

    public static WebApplicationBuilder AddSecurity(this WebApplicationBuilder builder)
    {
        var services = builder.Services;
        var options = builder.Configuration.GetSection(SecurityOptions.Section).Get<SecurityOptions>() ?? new SecurityOptions();
        var requireHttps = options.RequireHttps ?? !builder.Environment.IsDevelopment();
        var cookieSecurity = requireHttps ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
        services.AddSingleton(new SecuritySettings(requireHttps, options.TrustForwardedHeaders));

        if (options.TrustForwardedHeaders)
        {
            services.Configure<ForwardedHeadersOptions>(o =>
            {
                o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                foreach (var proxy in options.KnownProxies)
                {
                    o.KnownProxies.Add(IPAddress.Parse(proxy));
                }
            });
        }

        builder.WebHost.ConfigureKestrel(o =>
        {
            o.AddServerHeader = false;
            o.Limits.MaxRequestBodySize = MaxUploadBytes + 1024 * 1024;
        });
        services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = MaxUploadBytes);

        var dataProtection = services.AddDataProtection()
            .SetApplicationName("ClerkAssistant")
            .PersistKeysToDbContext<AppDbContext>();
        if (!string.IsNullOrWhiteSpace(options.DataProtectionCertificatePath))
        {
            dataProtection.ProtectKeysWithCertificate(X509CertificateLoader.LoadPkcs12FromFile(
                options.DataProtectionCertificatePath, options.DataProtectionCertificatePassword));
        }

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

        services.Configure<SecurityStampValidatorOptions>(o =>
        {
            o.ValidationInterval = TimeSpan.FromMinutes(1);
            o.OnRefreshingPrincipal = context =>
            {
                if (SessionLifetime.Find(context.CurrentPrincipal) is { } authTime && context.NewPrincipal?.Identity is ClaimsIdentity identity)
                {
                    identity.AddClaim(authTime);
                }

                return Task.CompletedTask;
            };
        });
        services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
        services.ConfigureApplicationCookie(o =>
        {
            o.Cookie.Name = requireHttps ? "__Host-udip.session" : "udip.session";
            o.Cookie.HttpOnly = true;
            o.Cookie.SameSite = SameSiteMode.Strict;
            o.Cookie.SecurePolicy = cookieSecurity;
            o.ExpireTimeSpan = TimeSpan.FromHours(8);
            o.SlidingExpiration = true;
            var validateSecurityStamp = o.Events.OnValidatePrincipal;
            o.Events.OnValidatePrincipal = async ctx =>
            {
                var now = ctx.HttpContext.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
                if (ctx.Principal is not null && SessionLifetime.IsExpired(ctx.Principal, now, TimeSpan.FromHours(options.MaxSessionHours)))
                {
                    ctx.RejectPrincipal();
                    await ctx.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
                    return;
                }

                await validateSecurityStamp(ctx);
            };
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
            o.Cookie.Name = requireHttps ? "__Host-udip.af" : "udip.af";
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
            o.AddPolicy(AiRateLimit, ctx => PerUser(ctx, 30));
            o.AddPolicy(UploadRateLimit, ctx => PerUser(ctx, 30));
            o.AddPolicy(SearchRateLimit, ctx => PerUser(ctx, 120));
        });

        return builder;
    }

    private static RateLimitPartition<string> PerUser(HttpContext ctx, int permitsPerMinute) =>
        RateLimitPartition.GetFixedWindowLimiter(
            ctx.User.Identity?.Name ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = permitsPerMinute, Window = TimeSpan.FromMinutes(1) });

    public static WebApplication UseSecurityHeaders(this WebApplication app)
    {
        var settings = app.Services.GetRequiredService<SecuritySettings>();
        if (settings.TrustForwardedHeaders)
        {
            app.UseForwardedHeaders();
        }

        if (settings.RequireHttps)
        {
            app.UseHsts();
            app.Use(async (context, next) =>
            {
                if (!context.Request.IsHttps && !context.Request.Path.StartsWithSegments("/health"))
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    await context.Response.WriteAsync("Wymagane jest połączenie HTTPS.");
                    return;
                }

                await next();
            });
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
