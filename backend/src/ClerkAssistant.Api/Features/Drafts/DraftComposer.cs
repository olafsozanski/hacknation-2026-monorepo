using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Features.Anonymization;
using ClerkAssistant.Api.Features.Search;
using ClerkAssistant.Api.Infrastructure.Ai;
using ClerkAssistant.Api.Infrastructure.Common;
using ClerkAssistant.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ClerkAssistant.Api.Features.Drafts;

public sealed record DraftComposition(string Content, bool AiUsed, List<string> Warnings, KnownPersonalData Requester);

public sealed partial class DraftComposer(
    AppDbContext db, AiGateway ai, HybridSearch search, IOptions<OrganizationOptions> organization, AppClock clock)
{
    public const string NoRepliesWarning = "Brak odpowiedzi wydziałów — projekt zawiera tylko szablon pisma.";
    public const int MaxContentLength = 200_000;

    private const int MaxReplyLength = 40_000;
    private const int MaxReplyInPrompt = 15_000;
    private const int MaxRequestInPrompt = 12_000;
    private const int MaxLegalContextInPrompt = 6_000;
    private const int LegalPassages = 3;
    private const string LegalBasis = "ustawy z dnia 6 września 2001 r. o dostępie do informacji publicznej";

    private static readonly CultureInfo Polish = CultureInfo.GetCultureInfo("pl-PL");

    private static readonly KnowledgeKind[] LegalKinds = [KnowledgeKind.Law, KnowledgeKind.Ordinance, KnowledgeKind.Guideline];

    public static string FormatDate(DateOnly date) => date.ToString("d MMMM yyyy", Polish) + " r.";

    public async Task<DraftComposition?> ComposeAsync(Guid requestId, CancellationToken ct)
    {
        var request = await db.Requests
            .Where(r => r.Id == requestId)
            .Select(r => new RequestFacts(r.CaseNumber, r.ReceivedOn, r.RequesterName, r.RequesterContact, r.Subject, r.Content))
            .FirstOrDefaultAsync(ct);
        if (request is null)
        {
            return null;
        }

        var replies = await db.RequestDocuments
            .Where(d => d.RequestId == requestId && d.Kind == DocumentKind.DepartmentReply)
            .OrderBy(d => d.UploadedAt)
            .Select(d => new DepartmentReply(
                d.Department != null ? d.Department.Name : null, d.File.FileName, d.File.ExtractedText))
            .ToListAsync(ct);

        var warnings = new List<string>();
        if (replies.Count == 0)
        {
            warnings.Add(NoRepliesWarning);
        }

        warnings.AddRange(replies
            .Where(r => string.IsNullOrWhiteSpace(r.Text))
            .Select(r => $"Nie odczytano tekstu z pliku „{r.FileName}” — uzupełnij tę część odpowiedzi ręcznie."));

        var body = await ModelBodyAsync(request, replies, warnings, ct);
        var content = Letter(request, body ?? TemplateBody(request, replies));
        if (content.Length > MaxContentLength)
        {
            content = content[..MaxContentLength];
            warnings.Add("Projekt był zbyt długi i został skrócony — sprawdź jego zakończenie.");
        }

        return new DraftComposition(
            content, body is not null, warnings, new KnownPersonalData(request.RequesterName, request.RequesterContact));
    }

    public static string CleanModelBody(string text)
    {
        var body = text.Replace("\r\n", "\n").Replace('\r', '\n');
        var opening = body.IndexOf("W odpowiedzi na wniosek", StringComparison.Ordinal);
        if (opening > 0)
        {
            body = body[opening..];
        }

        var closing = ClosingFormula().Match(body);
        if (closing.Success)
        {
            body = body[..closing.Index];
        }

        body = CodeFence().Replace(body, "");
        body = MarkdownHeading().Replace(body, "");
        body = MarkdownEmphasis().Replace(body, "$2");
        body = MarkdownBullet().Replace(body, "- ");
        body = ExcessBlankLines().Replace(body, "\n\n");
        return body.Trim();
    }

    private async Task<string?> ModelBodyAsync(
        RequestFacts request, List<DepartmentReply> replies, List<string> warnings, CancellationToken ct)
    {
        if (!ai.IsEnabled || !replies.Any(r => !string.IsNullOrWhiteSpace(r.Text)))
        {
            return null;
        }

        var (passages, searchFailed) = await search.FindPassagesAsync(
            new SearchRequest(EmbeddingIndexer.RequestText(request.Subject, request.Content))
            {
                KnowledgeKinds = LegalKinds,
                Limit = LegalPassages,
            },
            ct);
        var attempt = await ai.GetTextAsync("draft-response", SystemPrompt(), UserPrompt(request, replies, passages), ct);
        if (attempt.Failed || searchFailed)
        {
            warnings.Add(AiGateway.FallbackWarning);
        }

        var body = attempt.Value is null ? "" : CleanModelBody(attempt.Value);
        return body.Length == 0 ? null : body;
    }

    private string Letter(RequestFacts request, string body)
    {
        var addressee = string.IsNullOrWhiteSpace(request.RequesterName) ? "Wnioskodawca" : request.RequesterName.Trim();
        if (!string.IsNullOrWhiteSpace(request.RequesterContact))
        {
            addressee += "\n" + request.RequesterContact.Replace("\r\n", "\n").Trim();
        }

        return string.Join("\n\n",
            $"{organization.Value.City}, dnia {FormatDate(clock.Today)}",
            $"Znak sprawy: {request.CaseNumber}",
            addressee,
            $"Dotyczy: wniosku o udostępnienie informacji publicznej z dnia {FormatDate(request.ReceivedOn)}",
            body,
            "Z poważaniem",
            "[podpis osoby upoważnionej]");
    }

    private static string Opening(RequestFacts request) =>
        $"W odpowiedzi na wniosek z dnia {FormatDate(request.ReceivedOn)} o udostępnienie informacji publicznej, " +
        $"działając na podstawie {LegalBasis}, uprzejmie informuję, że";

    private static string TemplateBody(RequestFacts request, List<DepartmentReply> replies)
    {
        if (replies.Count == 0)
        {
            return $"{Opening(request)} [DO UZUPEŁNIENIA: treść odpowiedzi na wniosek].";
        }

        var body = new StringBuilder()
            .Append(Opening(request))
            .Append(" [DO UZUPEŁNIENIA: odpowiedź na poszczególne punkty wniosku, zredagowana na podstawie poniższych informacji].");
        foreach (var reply in replies)
        {
            body.Append("\n\n").Append("Informacja przekazana przez: ").Append(reply.Label);
            body.Append("\n\n").Append(string.IsNullOrWhiteSpace(reply.Text)
                ? $"[DO UZUPEŁNIENIA: treść informacji z pliku {reply.FileName}]"
                : PromptText.Truncate(ExcessBlankLines().Replace(reply.Text.Trim(), "\n\n"), MaxReplyLength));
        }

        return body.ToString();
    }

    private string SystemPrompt() => $"""
        Jesteś asystentem urzędnika ({organization.Value.Name}, {organization.Value.UnitName}) i przygotowujesz projekt odpowiedzi
        na wniosek o udostępnienie informacji publicznej. Projekt zostanie sprawdzony, poprawiony i podpisany przez urzędnika.
        Zasady:
        - Korzystaj wyłącznie z faktów zawartych w odpowiedziach wydziałów (<odpowiedz_wydzialu>). Kontekst prawny (<kontekst_prawny>) służy wyłącznie do prawidłowego powołania przepisów.
        - Nigdy nie wymyślaj liczb, kwot, dat, nazwisk ani nazw. Jeżeli brakuje informacji potrzebnej do odpowiedzi na którąś część wniosku, wstaw w tym miejscu znacznik [DO UZUPEŁNIENIA: opis brakującej informacji].
        - Odpowiedz na wniosek punkt po punkcie, w kolejności z wniosku.
        - Pisz formalną, urzędową polszczyzną, w pierwszej osobie liczby pojedynczej („uprzejmie informuję”).
        - Zwróć wyłącznie czysty tekst, bez Markdown (bez gwiazdek, krzyżyków i tabel). Akapity oddzielaj pustą linią.
        - Nie dodawaj miejscowości i daty, znaku sprawy, adresata, wiersza „Dotyczy”, formuły „Z poważaniem” ani podpisu — zostaną dodane automatycznie.
        - {PromptText.UntrustedDataRule}
        """;

    private static string UserPrompt(RequestFacts request, List<DepartmentReply> replies, KnowledgePassage[] passages)
    {
        var prompt = new StringBuilder()
            .AppendLine($"Znak sprawy: {request.CaseNumber}")
            .AppendLine($"Data wniosku: {FormatDate(request.ReceivedOn)}")
            .AppendLine($"Temat: {SearchText.CollapseWhitespace(request.Subject)}")
            .AppendLine()
            .AppendLine(PromptText.Wrap(
                "wniosek", string.IsNullOrWhiteSpace(request.Content) ? request.Subject : request.Content, MaxRequestInPrompt))
            .AppendLine();
        foreach (var reply in replies.Where(r => !string.IsNullOrWhiteSpace(r.Text)))
        {
            prompt.AppendLine(PromptText.Wrap("odpowiedz_wydzialu", reply.Text, MaxReplyInPrompt, reply.Label)).AppendLine();
        }

        if (passages.Length > 0)
        {
            var legal = string.Join("\n\n", passages.Select(p => $"{p.Title}:\n{p.Text}"));
            prompt.AppendLine(PromptText.Wrap("kontekst_prawny", legal, MaxLegalContextInPrompt)).AppendLine();
        }

        prompt.Append("Napisz treść pisma. Zacznij dokładnie od słów: „")
            .Append(Opening(request))
            .Append(" …” i zakończ przed formułą grzecznościową.");
        return prompt.ToString();
    }

    private sealed record RequestFacts(
        string CaseNumber,
        DateOnly ReceivedOn,
        string? RequesterName,
        string? RequesterContact,
        string Subject,
        string Content);

    private sealed record DepartmentReply(string? DepartmentName, string FileName, string Text)
    {
        public string Label => DepartmentName ?? $"komórkę merytoryczną (plik {FileName})";
    }

    [GeneratedRegex(@"^[ \t]*(?:Z\s+poważaniem|Z\s+wyrazami\s+szacunku)\b", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex ClosingFormula();

    [GeneratedRegex(@"^[ \t]*```[^\n]*\n?", RegexOptions.Multiline)]
    private static partial Regex CodeFence();

    [GeneratedRegex(@"^[ \t]*#{1,6}[ \t]*", RegexOptions.Multiline)]
    private static partial Regex MarkdownHeading();

    [GeneratedRegex(@"(\*\*|__)(.+?)\1")]
    private static partial Regex MarkdownEmphasis();

    [GeneratedRegex(@"^[ \t]*[*•][ \t]+", RegexOptions.Multiline)]
    private static partial Regex MarkdownBullet();

    [GeneratedRegex(@"\n[ \t]*\n(?:[ \t]*\n)+")]
    private static partial Regex ExcessBlankLines();
}
