using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Features.Anonymization;
using ClerkAssistant.Api.Infrastructure.Common;
using ClerkAssistant.Api.Infrastructure.Persistence;
using ClerkAssistant.Api.Infrastructure.Security;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace ClerkAssistant.Api.Features.Drafts;

public static class DraftsEndpoints
{
    public static IServiceCollection AddDraftsFeature(this IServiceCollection services) =>
        services
            .AddAnonymizationFeature()
            .AddScoped<DraftComposer>();

    public static void MapDraftsEndpoints(this RouteGroupBuilder api)
    {
        api.MapAnonymizationEndpoints();
        var group = api.MapGroup("/requests/{id:guid}/draft");
        group.MapGet("/", Get);
        group.MapPut("/", Save);
        group.MapPost("/generate", Generate).RequireRateLimiting(SecurityExtensions.AiRateLimit);
        group.MapPost("/detect", Detect).RequireRateLimiting(SecurityExtensions.AiRateLimit);
        group.MapGet("/export", Export);
    }

    private static async Task<Results<Ok<DraftDto>, NotFound>> Get(Guid id, AppDbContext db, CancellationToken ct)
    {
        var draft = await db.Drafts.AsNoTracking().FirstOrDefaultAsync(d => d.RequestId == id, ct);
        return draft is null ? TypedResults.NotFound() : TypedResults.Ok(await ToDtoAsync(db, draft, ct));
    }

    private static async Task<Results<Ok<GenerateDraftResult>, NotFound>> Generate(
        Guid id,
        DraftComposer composer,
        RedactionDetector detector,
        AppDbContext db,
        CurrentUser user,
        AuditLog audit,
        AppClock clock,
        CancellationToken ct)
    {
        var composition = await composer.ComposeAsync(id, ct);
        if (composition is null)
        {
            return TypedResults.NotFound();
        }

        var detection = await detector.DetectAsync(composition.Content, composition.Requester, ct);
        var draft = await FindOrAddDraftAsync(db, id, ct);
        draft.Content = composition.Content;
        draft.Redactions = detection.Suggestions;
        draft.GeneratedByAi = composition.AiUsed;
        draft.UpdatedAt = clock.Now;
        draft.UpdatedById = user.Id;
        audit.Record("draft.generated", nameof(InfoRequest), id, composition.AiUsed ? "AI" : "szablon");
        await db.SaveChangesAsync(ct);

        return TypedResults.Ok(new GenerateDraftResult(
            await ToDtoAsync(db, draft, ct),
            composition.Warnings.Concat(detection.Warnings).Distinct().ToArray(),
            composition.AiUsed || detection.AiUsed));
    }

    private static async Task<Results<Ok<DraftDto>, NotFound>> Save(
        Guid id,
        SaveDraftRequest request,
        AppDbContext db,
        CurrentUser user,
        AuditLog audit,
        AppClock clock,
        CancellationToken ct)
    {
        if (!await db.Requests.AnyAsync(r => r.Id == id, ct))
        {
            return TypedResults.NotFound();
        }

        var draft = await FindOrAddDraftAsync(db, id, ct);
        if (db.Entry(draft).State != EntityState.Added && request.Version is { } version)
        {
            db.Entry(draft).Property(d => d.Version).OriginalValue = version;
        }

        draft.Content = request.Content;
        draft.Redactions = RedactionSet.Sanitize(request.Content, request.Redactions);
        draft.UpdatedAt = clock.Now;
        draft.UpdatedById = user.Id;
        var approved = draft.Redactions.Count(r => r.Status == RedactionStatus.Approved);
        audit.Record("draft.saved", nameof(InfoRequest), id, $"zatwierdzone anonimizacje: {approved}");
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await ToDtoAsync(db, draft, ct));
    }

    private static async Task<Results<Ok<DetectRedactionsResult>, NotFound>> Detect(
        Guid id, DetectRedactionsRequest request, AppDbContext db, RedactionDetector detector, CancellationToken ct)
    {
        var known = await db.Requests
            .Where(r => r.Id == id)
            .Select(r => new KnownPersonalData(r.RequesterName, r.RequesterContact))
            .FirstOrDefaultAsync(ct);
        if (known is null)
        {
            return TypedResults.NotFound();
        }

        var existing = request.Existing
            .OfType<Redaction>()
            .Where(r => Enum.IsDefined(r.Category) && Enum.IsDefined(r.Status) && Enum.IsDefined(r.Source));
        var placed = RedactionSet.Relocate(request.Content, existing);
        var detection = await detector.DetectAsync(request.Content, known, ct);
        return TypedResults.Ok(new DetectRedactionsResult(
            RedactionSet.AddSuggestions(placed, detection.Suggestions).ToArray(),
            detection.Warnings,
            detection.AiUsed));
    }

    private static async Task<Results<FileContentHttpResult, NotFound, ValidationProblem>> Export(
        Guid id, string? format, AppDbContext db, AuditLog audit, CancellationToken ct)
    {
        if (!DraftExport.TryParseFormat(format, out var extension))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["format"] = [DraftExport.UnsupportedFormatMessage],
            });
        }

        var caseNumber = await db.Requests.Where(r => r.Id == id).Select(r => r.CaseNumber).FirstOrDefaultAsync(ct);
        var draft = await db.Drafts
            .Where(d => d.RequestId == id)
            .Select(d => new { d.Content, d.Redactions })
            .FirstOrDefaultAsync(ct);
        if (caseNumber is null || draft is null)
        {
            return TypedResults.NotFound();
        }

        var text = DraftExport.PrepareText(draft.Content, draft.Redactions);
        var bytes = extension == DraftExport.Docx
            ? DraftExport.ToDocx(text, $"Odpowiedź na wniosek {caseNumber}")
            : DraftExport.ToTxt(text);
        audit.Record("draft.exported", nameof(InfoRequest), id, extension);
        await db.SaveChangesAsync(ct);
        return TypedResults.File(
            bytes,
            DraftExport.ContentType(extension),
            DraftExport.FileName(caseNumber, extension));
    }

    private static async Task<ResponseDraft> FindOrAddDraftAsync(AppDbContext db, Guid requestId, CancellationToken ct)
    {
        var draft = await db.Drafts.FirstOrDefaultAsync(d => d.RequestId == requestId, ct);
        if (draft is null)
        {
            draft = new ResponseDraft { RequestId = requestId };
            db.Drafts.Add(draft);
        }

        return draft;
    }

    private static async Task<DraftDto> ToDtoAsync(AppDbContext db, ResponseDraft draft, CancellationToken ct)
    {
        var updatedBy = await db.Users
            .Where(u => u.Id == draft.UpdatedById)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync(ct);
        return new DraftDto(
            draft.Content,
            draft.Redactions.ToArray(),
            draft.GeneratedByAi,
            draft.UpdatedAt,
            updatedBy ?? "",
            draft.Version);
    }
}
