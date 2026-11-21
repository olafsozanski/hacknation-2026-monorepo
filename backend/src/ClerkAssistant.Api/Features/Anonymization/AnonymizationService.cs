using ClerkAssistant.Api.Features.Drafts;
using ClerkAssistant.Api.Infrastructure.Common;
using ClerkAssistant.Api.Infrastructure.Documents;
using ClerkAssistant.Api.Infrastructure.Persistence;

namespace ClerkAssistant.Api.Features.Anonymization;

public sealed class AnonymizationService(
    RedactionDetector detector, DocumentReader reader, AppDbContext db, AuditLog audit)
{
    public const string AuditEntityType = "Document";
    public const string EmptyTextWarning = "Nie odczytano tekstu z dokumentu — może to być skan. Wklej treść ręcznie.";
    public const string NothingFoundWarning =
        "Nie znaleziono danych do anonimizacji — przed udostępnieniem przejrzyj dokument samodzielnie.";

    public async Task<AnonymizationResult> AnalyzeFileAsync(IFormFile? file, CancellationToken ct)
    {
        if (file is null)
        {
            throw ValidationException.For("file", "Wybierz plik do anonimizacji.");
        }

        var document = await reader.ReadAsync(file, ct);
        return await AnalyzeAsync(document.FileName, document.Text, "plik", ct);
    }

    public Task<AnonymizationResult> AnalyzeTextAsync(AnalyzeTextRequest request, CancellationToken ct) =>
        AnalyzeAsync(null, request.Text.Replace("\r\n", "\n").Replace('\r', '\n'), "tekst", ct);

    public async Task<FileDownload> ExportAsync(AnonymizationExportRequest request, CancellationToken ct)
    {
        if (!DraftExport.TryParseFormat(request.Format, out var format))
        {
            throw ValidationException.For("format", DraftExport.UnsupportedFormatMessage);
        }

        var text = DraftExport.PrepareText(request.Content, RedactionSet.Sanitize(request.Content, request.Redactions));
        audit.Record("anonymization.exported", AuditEntityType, null, format);
        await db.SaveChangesAsync(ct);
        return DraftExport.ToFile(text, format, "Dokument zanonimizowany", DraftExport.AnonymizedFileName(request.FileName, format));
    }

    private async Task<AnonymizationResult> AnalyzeAsync(string? fileName, string text, string origin, CancellationToken ct)
    {
        var outcome = string.IsNullOrWhiteSpace(text)
            ? new DetectionOutcome([], [EmptyTextWarning], false)
            : await detector.DetectAsync(text, null, ct);
        if (outcome.Suggestions.Count == 0 && !string.IsNullOrWhiteSpace(text))
        {
            outcome = outcome with { Warnings = [.. outcome.Warnings, NothingFoundWarning] };
        }

        audit.Record("anonymization.analyzed", AuditEntityType, null, origin);
        await db.SaveChangesAsync(ct);
        return outcome.ToResult(fileName, text);
    }
}
