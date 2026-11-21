using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Features.Anonymization;
using ClerkAssistant.Api.Features.Search;
using ClerkAssistant.Api.Infrastructure.Common;
using ClerkAssistant.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClerkAssistant.Api.Features.Drafts;

public sealed class DraftService(
    AppDbContext db,
    DraftComposer composer,
    RedactionDetector detector,
    CurrentUser user,
    AuditLog audit,
    AppClock clock)
{
    public const string DraftNotFound = "Nie znaleziono projektu odpowiedzi.";
    public const string NothingDetectedWarning =
        "Nie wykryto danych do anonimizacji — przed wysłaniem przejrzyj pismo samodzielnie.";

    private const int NonTrivialDraftLength = 200;

    public async Task<DraftDto> GetAsync(Guid requestId, CancellationToken ct)
    {
        var draft = await db.Drafts.AsNoTracking().FirstOrDefaultAsync(d => d.RequestId == requestId, ct)
            ?? throw new NotFoundException(DraftNotFound);
        return await ToDtoAsync(draft, ct);
    }

    public async Task<GenerateDraftResult> GenerateAsync(Guid requestId, uint? version, CancellationToken ct)
    {
        var existing = await db.Drafts.FirstOrDefaultAsync(d => d.RequestId == requestId, ct);
        if (existing is not null)
        {
            RequireCurrentVersion(existing, version);
            db.Entry(existing).Property(d => d.Version).OriginalValue = existing.Version;
        }

        var composition = await composer.ComposeAsync(requestId, ct)
            ?? throw new NotFoundException(SearchService.RequestNotFound);
        var detection = await detector.DetectAsync(composition.Content, composition.Requester, composition.Sources, ct);
        var draft = existing ?? db.Drafts.Add(new ResponseDraft { RequestId = requestId }).Entity;
        draft.Content = composition.Content;
        draft.Redactions = detection.Suggestions;
        draft.GeneratedByAi = composition.AiUsed;
        Touch(draft);
        audit.Record("draft.generated", nameof(InfoRequest), requestId, composition.AiUsed ? "AI" : "szablon");
        await db.SaveChangesAsync(ct);

        var warnings = composition.Warnings.Concat(Warnings(detection, composition.Content)).Distinct().ToArray();
        return (await ToDtoAsync(draft, ct)).ToGenerateResult(warnings, composition.AiUsed || detection.AiUsed);
    }

    public async Task<DraftDto> SaveAsync(Guid requestId, SaveDraftRequest request, CancellationToken ct)
    {
        if (!await db.Requests.AnyAsync(r => r.Id == requestId, ct))
        {
            throw new NotFoundException(SearchService.RequestNotFound);
        }

        var draft = await db.Drafts.FirstOrDefaultAsync(d => d.RequestId == requestId, ct);
        if (draft is null)
        {
            draft = db.Drafts.Add(new ResponseDraft { RequestId = requestId }).Entity;
        }
        else if (request.Version is { } version)
        {
            db.Entry(draft).Property(d => d.Version).OriginalValue = version;
        }

        draft.Content = request.Content;
        draft.Redactions = RedactionSet.Sanitize(request.Content, request.Redactions);
        Touch(draft);
        var approved = draft.Redactions.Count(r => r.Status == RedactionStatus.Approved);
        audit.Record("draft.saved", nameof(InfoRequest), requestId, $"zatwierdzone anonimizacje: {approved}");
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(draft, ct);
    }

    public async Task<DetectRedactionsResult> DetectAsync(Guid requestId, DetectRedactionsRequest request, CancellationToken ct)
    {
        var source = await db.Requests
            .Where(r => r.Id == requestId)
            .Select(r => new
            {
                r.RequesterName,
                r.RequesterContact,
                r.Content,
                Replies = r.Documents.Where(d => d.Kind == DocumentKind.DepartmentReply).Select(d => d.File.ExtractedText).ToList(),
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(SearchService.RequestNotFound);

        var existing = request.Existing
            .OfType<Redaction>()
            .Where(r => Enum.IsDefined(r.Category) && Enum.IsDefined(r.Status) && Enum.IsDefined(r.Source));
        var placed = RedactionSet.Relocate(request.Content, existing, ct);
        var detection = await detector.DetectAsync(
            request.Content,
            new KnownPersonalData(source.RequesterName, source.RequesterContact),
            [source.Content, .. source.Replies],
            ct);
        return (detection with
        {
            Suggestions = RedactionSet.AddSuggestions(placed, detection.Suggestions, ct),
            Warnings = Warnings(detection, request.Content).ToArray(),
        }).ToDetectResult();
    }

    public async Task<FileDownload> ExportAsync(Guid requestId, string? format, CancellationToken ct)
    {
        if (!DraftExport.TryParseFormat(format, out var extension))
        {
            throw ValidationException.For("format", DraftExport.UnsupportedFormatMessage);
        }

        var caseNumber = await db.Requests.Where(r => r.Id == requestId).Select(r => r.CaseNumber).FirstOrDefaultAsync(ct);
        var draft = await db.Drafts
            .Where(d => d.RequestId == requestId)
            .Select(d => new { d.Content, d.Redactions })
            .FirstOrDefaultAsync(ct);
        if (caseNumber is null || draft is null)
        {
            throw new NotFoundException(DraftNotFound);
        }

        var text = DraftExport.PrepareText(draft.Content, draft.Redactions);
        audit.Record("draft.exported", nameof(InfoRequest), requestId, extension);
        await db.SaveChangesAsync(ct);
        return DraftExport.ToFile(
            text, extension, $"Odpowiedź na wniosek {caseNumber}", DraftExport.FileName(caseNumber, extension));
    }

    private static void RequireCurrentVersion(ResponseDraft draft, uint? version)
    {
        if (version != draft.Version)
        {
            throw new DbUpdateConcurrencyException("The draft was changed since the client loaded it.");
        }
    }

    private static IEnumerable<string> Warnings(DetectionOutcome detection, string content) =>
        detection.AiFoundNothing && content.Trim().Length >= NonTrivialDraftLength
            ? [.. detection.Warnings, NothingDetectedWarning]
            : detection.Warnings;

    private void Touch(ResponseDraft draft)
    {
        draft.UpdatedAt = clock.Now;
        draft.UpdatedById = user.Id;
    }

    private async Task<DraftDto> ToDtoAsync(ResponseDraft draft, CancellationToken ct)
    {
        var updatedBy = await db.Users
            .Where(u => u.Id == draft.UpdatedById)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync(ct);
        return draft.ToDto(updatedBy ?? "");
    }
}
