using System.Globalization;
using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Features.Requests;
using ClerkAssistant.Api.Features.Routing;
using ClerkAssistant.Api.Features.Search;
using ClerkAssistant.Api.Infrastructure.Ai;
using ClerkAssistant.Api.Infrastructure.Common;
using ClerkAssistant.Api.Infrastructure.Documents;
using ClerkAssistant.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ClerkAssistant.Api.Features.Intake;

public sealed class IntakeService(
    AppDbContext db,
    AiGateway ai,
    HybridSearch search,
    DepartmentRouter router,
    CaseNumberGenerator caseNumbers,
    DocumentReader reader,
    CurrentUser user,
    AppClock clock,
    IOptions<OrganizationOptions> organization)
{
    public const string EmptyTextWarning = "Nie odczytano tekstu z dokumentu — może to być skan. Wpisz treść ręcznie.";
    public const string NotARequestWarning =
        "Pismo może nie być wnioskiem o udostępnienie informacji publicznej — sprawdź uwagi przed rejestracją.";

    private const int SimilarCasesLimit = 5;
    private const int MaxTextInPrompt = 12_000;

    public async Task<IntakeResult> AnalyzeFileAsync(IFormFile? file, CancellationToken ct)
    {
        if (file is null)
        {
            throw ValidationException.For("file", "Wybierz plik z wnioskiem.");
        }

        var document = await reader.ReadAsync(file, ct);
        await UnattachedFiles.DeleteStaleAsync(db, clock.Now, ct);
        var stored = document.ToStoredFile(user.Id, clock.Now);
        db.Files.Add(stored);
        await db.SaveChangesAsync(ct);
        return await AnalyzeAsync(document.Text, stored.Id, stored.FileName, ct);
    }

    public Task<IntakeResult> AnalyzeTextAsync(IntakeTextRequest request, CancellationToken ct) =>
        AnalyzeAsync(request.Text.Replace("\r\n", "\n").Replace('\r', '\n').Trim(), null, null, ct);

    private async Task<IntakeResult> AnalyzeAsync(string text, Guid? fileId, string? fileName, CancellationToken ct)
    {
        var today = clock.Today;
        var (_, caseNumber) = await caseNumbers.NextAsync(today.Year, ct);
        var dueOn = Deadlines.InitialDueDate(today);
        if (string.IsNullOrWhiteSpace(text))
        {
            return RequestExtraction.Empty.ToResult(
                fileId, fileName, text, caseNumber, today, dueOn, [], [], [EmptyTextWarning], false);
        }

        var heuristic = IntakeHeuristics.Analyze(
            text, today, await InstitutionalDomainsAsync(ct), organization.Value.Address);
        var extraction = ai.GetStructuredAsync<IntakeExtraction>(
            "extract-intake", SystemPrompt(today), PromptText.Wrap("dokument", text, MaxTextInPrompt), ct);

        var embedding = await search.EmbedAsync(text, ct);
        var similar = await search.SearchAsync(
            SearchService.SimilarCasesQuery(text, today, SimilarCasesLimit) with { Embedding = embedding }, ct);
        var routing = await router.SuggestAsync(text, null, embedding, ct);
        var attempt = await extraction;

        var suggestion = attempt.Value is { } value ? Combine(value, heuristic, today) : heuristic;
        var warnings = new List<string>();
        if (embedding.Failed || similar.AiFailed || attempt.Failed || routing.AiFailed)
        {
            warnings.Add(AiGateway.FallbackWarning);
        }

        if (!suggestion.LooksLikePublicInformationRequest)
        {
            warnings.Add(NotARequestWarning);
        }

        return suggestion.ToResult(
            fileId,
            fileName,
            text,
            caseNumber,
            today,
            dueOn,
            similar.Hits,
            routing.Suggestions,
            warnings.ToArray(),
            attempt.Succeeded || routing.AiUsed);
    }

    private static RequestExtraction Combine(IntakeExtraction model, RequestExtraction heuristic, DateOnly today)
    {
        var items = (model.RequestedItems ?? [])
            .Select(i => Limit(i, IntakeHeuristics.MaxItemLength))
            .OfType<string>()
            .Take(IntakeHeuristics.MaxItems)
            .ToArray();
        return new RequestExtraction(
            IntakeHeuristics.CleanSubject(model.Subject) ?? heuristic.Subject,
            Limit(model.RequesterName, 300) ?? heuristic.RequesterName,
            heuristic.RequesterContact ?? Limit(model.RequesterContact, 500),
            ParseChannel(model.ReplyChannel) ?? heuristic.ReplyChannel,
            ParseModelDate(model.RequestDate, today) ?? heuristic.RequestDate,
            items.Length > 0 ? items : heuristic.RequestedItems,
            model.IsPublicInformationRequest,
            Limit(model.Remarks, 1000) ?? (model.IsPublicInformationRequest ? null : heuristic.Remarks));
    }

    private static string? Limit(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Equals("null", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var clean = SearchText.CollapseWhitespace(value);
        return clean.Length <= maxLength ? clean : clean[..maxLength].TrimEnd();
    }

    private static ReplyChannel? ParseChannel(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "email" or "e-mail" or "mail" => ReplyChannel.Email,
        "post" or "poczta" or "list" => ReplyChannel.Post,
        "epuap" => ReplyChannel.Epuap,
        "inperson" or "osobiście" or "osobiscie" => ReplyChannel.InPerson,
        _ => null,
    };

    private static DateOnly? ParseModelDate(string? value, DateOnly today) =>
        DateOnly.TryParseExact(value?.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
        && date <= today.AddDays(7) && date >= today.AddYears(-5)
            ? date
            : null;

    private async Task<string[]> InstitutionalDomainsAsync(CancellationToken ct)
    {
        var emails = await db.Departments.Where(d => d.Email != null).Select(d => d.Email!).ToListAsync(ct);
        return emails
            .Select(e => e[(e.IndexOf('@') + 1)..].Trim().ToLowerInvariant())
            .Where(d => d.Contains('.'))
            .Distinct()
            .ToArray();
    }

    private string SystemPrompt(DateOnly today) => $"""
        Jesteś asystentem urzędnika ({organization.Value.Name}). Analizujesz pismo, które wpłynęło do urzędu,
        i przygotowujesz dane do rejestru wniosków o udostępnienie informacji publicznej (ustawa z dnia 6 września 2001 r.
        o dostępie do informacji publicznej). Dzisiejsza data: {today:yyyy-MM-dd}.
        Nie zgaduj: jeżeli jakiejś informacji nie ma w piśmie, zwróć null.
        Zwróć:
        - subject: zwięzły temat sprawy po polsku, w stylu nominalnym, najwyżej 200 znaków, np. „Koszty organizacji Sylwestra Miejskiego 2025”;
        - requesterName: imię i nazwisko albo nazwę wnioskodawcy;
        - requesterContact: adres e-mail albo adres pocztowy wnioskodawcy, na który należy udzielić odpowiedzi;
        - replyChannel: preferowany sposób udzielenia odpowiedzi, jeśli wnioskodawca go wskazał: "email", "post" albo "epuap";
        - requestDate: datę sporządzenia pisma w formacie RRRR-MM-DD, jeśli została podana;
        - requestedItems: listę informacji, o które wnosi wnioskodawca — każdą osobno, zwięźle;
        - isPublicInformationRequest: true, jeśli pismo jest wnioskiem o udostępnienie informacji publicznej;
        - remarks: krótkie uwagi dla urzędnika, np. „Wniosek może dotyczyć informacji przetworzonej” albo „To raczej petycja lub skarga”; null, jeśli brak uwag.
        {PromptText.UntrustedDataRule}
        """;
}
