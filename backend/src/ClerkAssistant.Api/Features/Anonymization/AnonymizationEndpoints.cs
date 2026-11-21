using ClerkAssistant.Api.Features.Drafts;
using ClerkAssistant.Api.Infrastructure.Ai;
using ClerkAssistant.Api.Infrastructure.Common;
using ClerkAssistant.Api.Infrastructure.Documents;
using ClerkAssistant.Api.Infrastructure.Persistence;
using ClerkAssistant.Api.Infrastructure.Security;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ClerkAssistant.Api.Features.Anonymization;

public static class AnonymizationEndpoints
{
    public const string ExportAuditEntityType = "Document";
    public const string EmptyTextWarning = "Nie odczytano tekstu z dokumentu — może to być skan. Wklej treść ręcznie.";
    public const string NothingFoundWarning =
        "Nie znaleziono danych do anonimizacji — przed udostępnieniem przejrzyj dokument samodzielnie.";

    public static IServiceCollection AddAnonymizationFeature(this IServiceCollection services)
    {
        services.TryAddSingleton<AiGateway>();
        services.TryAddSingleton<PublicContactFilter>();
        services.TryAddSingleton<RedactionDetector>();
        services.TryAddSingleton<RouteRegistration>();
        return services;
    }

    public static void MapAnonymizationEndpoints(this RouteGroupBuilder api)
    {
        if (!((IEndpointRouteBuilder)api).ServiceProvider.GetRequiredService<RouteRegistration>().TryClaim())
        {
            return;
        }

        var group = api.MapGroup("/anonymization");
        group.MapPost("/analyze", AnalyzeFile).RequireRateLimiting(SecurityExtensions.AiRateLimit);
        group.MapPost("/analyze-text", AnalyzeText).RequireRateLimiting(SecurityExtensions.AiRateLimit);
        group.MapPost("/export", Export);
    }

    private static async Task<Results<Ok<AnonymizationResult>, ValidationProblem>> AnalyzeFile(
        IFormFile? file, DocumentReader reader, RedactionDetector detector, CancellationToken ct)
    {
        if (file is null)
        {
            return Invalid("file", "Wybierz plik do anonimizacji.");
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

        return TypedResults.Ok(await AnalyzeAsync(document.FileName, document.Text, detector, ct));
    }

    private static async Task<Ok<AnonymizationResult>> AnalyzeText(
        AnalyzeTextRequest request, RedactionDetector detector, CancellationToken ct)
    {
        var text = request.Text.Replace("\r\n", "\n").Replace('\r', '\n');
        return TypedResults.Ok(await AnalyzeAsync(null, text, detector, ct));
    }

    private static async Task<Results<FileContentHttpResult, ValidationProblem>> Export(
        AnonymizationExportRequest request, AppDbContext db, AuditLog audit, CancellationToken ct)
    {
        if (!DraftExport.TryParseFormat(request.Format, out var format))
        {
            return Invalid("format", DraftExport.UnsupportedFormatMessage);
        }

        var text = DraftExport.PrepareText(request.Content, RedactionSet.Sanitize(request.Content, request.Redactions));
        var bytes = format == DraftExport.Docx ? DraftExport.ToDocx(text, "Dokument zanonimizowany") : DraftExport.ToTxt(text);
        audit.Record("anonymization.exported", ExportAuditEntityType, null, format);
        await db.SaveChangesAsync(ct);
        return TypedResults.File(bytes, DraftExport.ContentType(format), DraftExport.AnonymizedFileName(request.FileName, format));
    }

    private static async Task<AnonymizationResult> AnalyzeAsync(
        string? fileName, string text, RedactionDetector detector, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new AnonymizationResult(fileName, text, [], [EmptyTextWarning], false);
        }

        var outcome = await detector.DetectAsync(text, null, ct);
        var warnings = outcome.Suggestions.Count == 0 ? [.. outcome.Warnings, NothingFoundWarning] : outcome.Warnings;
        return new AnonymizationResult(fileName, text, outcome.Suggestions.ToArray(), warnings, outcome.AiUsed);
    }

    private static ValidationProblem Invalid(string field, string message) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });

    private sealed class RouteRegistration
    {
        private int _mapped;

        public bool TryClaim() => Interlocked.Exchange(ref _mapped, 1) == 0;
    }
}
