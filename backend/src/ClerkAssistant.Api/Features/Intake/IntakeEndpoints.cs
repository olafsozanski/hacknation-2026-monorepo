using ClerkAssistant.Api.Infrastructure.Ai;
using ClerkAssistant.Api.Infrastructure.Common;
using ClerkAssistant.Api.Infrastructure.Documents;
using ClerkAssistant.Api.Infrastructure.Persistence;
using ClerkAssistant.Api.Infrastructure.Security;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ClerkAssistant.Api.Features.Intake;

public static class IntakeEndpoints
{
    public static readonly TimeSpan UnattachedFileLifetime = TimeSpan.FromHours(24);

    public static IServiceCollection AddIntakeFeature(this IServiceCollection services)
    {
        services.TryAddSingleton<AiGateway>();
        services.AddScoped<IntakeAnalyzer>();
        return services;
    }

    public static void MapIntakeEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/intake").RequireRateLimiting(SecurityExtensions.AiRateLimit);
        group.MapPost("/", AnalyzeFile);
        group.MapPost("/text", AnalyzeText);
    }

    private static async Task<Results<Ok<IntakeResult>, ValidationProblem>> AnalyzeFile(
        IFormFile? file,
        DocumentReader reader,
        IntakeAnalyzer analyzer,
        AppDbContext db,
        CurrentUser user,
        AppClock clock,
        CancellationToken ct)
    {
        if (file is null)
        {
            return Invalid("file", "Wybierz plik z wnioskiem.");
        }

        ReadDocument document;
        try
        {
            document = await reader.ReadAsync(file, ct);
        }
        catch (DocumentRejectedException ex)
        {
            return Invalid("file", ex.Message);
        }

        await DeleteStaleUnattachedFilesAsync(db, clock, ct);
        var stored = document.ToStoredFile(user.Id, clock.Now);
        db.Files.Add(stored);
        await db.SaveChangesAsync(ct);

        return TypedResults.Ok(await analyzer.AnalyzeAsync(document.Text, stored.Id, stored.FileName, ct));
    }

    private static async Task<Ok<IntakeResult>> AnalyzeText(
        IntakeTextRequest request, IntakeAnalyzer analyzer, CancellationToken ct)
    {
        var text = request.Text.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
        return TypedResults.Ok(await analyzer.AnalyzeAsync(text, null, null, ct));
    }

    private static Task DeleteStaleUnattachedFilesAsync(AppDbContext db, AppClock clock, CancellationToken ct)
    {
        var cutoff = clock.Now - UnattachedFileLifetime;
        return db.Files
            .Where(f => f.CreatedAt < cutoff
                        && !db.RequestDocuments.Any(d => d.FileId == f.Id)
                        && !db.KnowledgeDocuments.Any(k => k.FileId == f.Id))
            .ExecuteDeleteAsync(ct);
    }

    private static ValidationProblem Invalid(string field, string message) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });
}
