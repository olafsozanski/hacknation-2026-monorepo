using System.Text.Json.Serialization;
using ClerkAssistant.Api.Features;
using ClerkAssistant.Api.Infrastructure.Ai;
using ClerkAssistant.Api.Infrastructure.Common;
using ClerkAssistant.Api.Infrastructure.Documents;
using ClerkAssistant.Api.Infrastructure.Persistence;
using ClerkAssistant.Api.Infrastructure.Security;
using ClerkAssistant.Api.Infrastructure.Seeding;
using Microsoft.EntityFrameworkCore;

AppDomain.CurrentDomain.SetData("REGEX_DEFAULT_MATCH_TIMEOUT", TimeSpan.FromSeconds(2));

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(o => o
    .UseNpgsql(builder.Configuration.GetConnectionString("Database"), npgsql => npgsql.UseVector())
    .UseSnakeCaseNamingConvention());

builder.AddSecurity();

builder.Services.Configure<OrganizationOptions>(builder.Configuration.GetSection(OrganizationOptions.Section));
builder.Services.Configure<AiOptions>(builder.Configuration.GetSection(AiOptions.Section));
builder.Services.Configure<SeedOptions>(builder.Configuration.GetSection(SeedOptions.Section));
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));
builder.Services.AddHttpContextAccessor();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddValidation();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<AppClock>();
builder.Services.AddSingleton<AiClients>();
builder.Services.AddSingleton<DocumentReader>();
builder.Services.AddScoped<CurrentUser>();
builder.Services.AddScoped<AuditLog>();
builder.Services.AddScoped<DemoDataSeeder>();
builder.Services.AddFeatures();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSecurityHeaders();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.UseAntiforgery();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.MapHealthChecks("/health").AllowAnonymous();
app.MapFeatures();
app.MapFallbackToFile("index.html").AllowAnonymous();

await app.InitializeDatabaseAsync();
app.Run();

public partial class Program;
