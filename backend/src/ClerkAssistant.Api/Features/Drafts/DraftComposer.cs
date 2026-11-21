using System.Text;
using System.Text.RegularExpressions;
using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Features.Anonymization;
using ClerkAssistant.Api.Features.Search;
using ClerkAssistant.Api.Infrastructure.Ai;
using ClerkAssistant.Api.Infrastructure.Common;
using ClerkAssistant.Api.Infrastructure.Letters;
using ClerkAssistant.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ClerkAssistant.Api.Features.Drafts;

public sealed record DraftComposition(string Content, bool AiUsed, List<string> Warnings, KnownPersonalData Requester);

internal sealed record DraftAnswer(string? Addressee, double? AddresseeConfidence, string? Body);

public sealed partial class DraftComposer(
    AppDbContext db, AiGateway ai, HybridSearch search, IOptions<OrganizationOptions> organization, AppClock clock)
{
    public const string NoRepliesWarning = "Brak odpowiedzi wydziałów — projekt zawiera tylko szablon pisma.";
    public const string NotHolderWarning =
        "Z odpowiedzi wydziału wynika, że Urząd nie posiada wnioskowanych informacji — przygotowano wzór pisma wskazującego właściwy podmiot (art. 4 ust. 3 ustawy).";
    public const int MaxContentLength = 200_000;
    public const double AddresseeOverrideConfidence = 0.75;

    private const int MaxReplyLength = 40_000;
    private const int MaxReplyInPrompt = 15_000;
    private const int MaxRequestInPrompt = 12_000;
    private const int MaxLegalContextInPrompt = 6_000;
    private const int LegalPassages = 3;

    private static readonly KnowledgeKind[] LegalKinds = [KnowledgeKind.Law, KnowledgeKind.Ordinance, KnowledgeKind.Guideline];

    private OrganizationOptions Organization => organization.Value;

    public async Task<DraftComposition?> ComposeAsync(Guid requestId, CancellationToken ct)
    {
        var request = await db.Requests
            .Where(r => r.Id == requestId)
            .Select(r => new RequestFacts(
                r.CaseNumber, r.ReceivedOn, r.RequesterName, r.RequesterContact, r.ReplyChannel, r.Subject, r.Content))
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
        var assigned = await db.RequestDepartments
            .Where(rd => rd.RequestId == requestId)
            .OrderBy(rd => rd.Department.Name)
            .Select(rd => rd.Department.Name)
            .ToListAsync(ct);

        var warnings = new List<string>();
        if (replies.Count == 0)
        {
            warnings.Add(NoRepliesWarning);
        }

        warnings.AddRange(replies
            .Where(r => string.IsNullOrWhiteSpace(r.Text))
            .Select(r => $"Nie odczytano tekstu z pliku „{r.FileName}” — uzupełnij tę część odpowiedzi ręcznie."));

        var departments = replies.Select(r => r.DepartmentName).OfType<string>().Distinct().ToList();
        var answered = replies.Where(r => !string.IsNullOrWhiteSpace(r.Text)).ToList();
        var notHolder = answered.Count > 0 && answered.All(r => IsNotHolderReply(r.Text));
        if (notHolder)
        {
            warnings.Add(NotHolderWarning);
        }

        var letter = new LetterFacts(
            request,
            departments.Count > 0 ? departments : assigned,
            LetterLayout.GuessAddressee(request.RequesterName),
            notHolder);

        var answer = await ModelAnswerAsync(letter, replies, warnings, ct);
        var addressee = answer?.Addressee ?? letter.Addressee;
        var body = answer?.Body ?? TemplateBody(letter with { Addressee = addressee }, replies);
        var content = Letter(request, addressee, MatchAddresseeForm(body, addressee));
        if (content.Length > MaxContentLength)
        {
            content = content[..MaxContentLength];
            warnings.Add("Projekt był zbyt długi i został skrócony — sprawdź jego zakończenie.");
        }

        return new DraftComposition(
            content, answer is not null, warnings, new KnownPersonalData(request.RequesterName, request.RequesterContact));
    }

    public static string CleanModelBody(string text)
    {
        var body = text.Replace("\r\n", "\n").Replace('\r', '\n');
        var opening = BodyOpening().Match(body);
        if (opening.Success && opening.Index > 0)
        {
            body = body[opening.Index..];
        }

        body = LeadingSalutation().Replace(body, "");
        var closing = ClosingFormula().Match(body);
        if (closing.Success)
        {
            body = body[..closing.Index];
        }

        body = CodeFence().Replace(body, "");
        body = MarkdownHeading().Replace(body, "");
        body = MarkdownEmphasis().Replace(body, "$2");
        body = MarkdownBullet().Replace(body, "- ");
        body = ExcessBlankLines().Replace(body, "\n\n").Trim();
        return LowercaseOpening().IsMatch(body) ? char.ToLowerInvariant(body[0]) + body[1..] : body;
    }

    public static string MatchAddresseeForm(string body, Addressee addressee)
    {
        var form = AddresseeForm().Match(body);
        return form.Success && form.Index < 400
            ? body[..form.Index] + LetterLayout.ByYou(addressee) + body[(form.Index + form.Length)..]
            : body;
    }

    public static bool IsNotHolderReply(string text) => Redirect().IsMatch(text);

    private async Task<ModelAnswer?> ModelAnswerAsync(
        LetterFacts letter, List<DepartmentReply> replies, List<string> warnings, CancellationToken ct)
    {
        if (!ai.IsEnabled || !replies.Any(r => !string.IsNullOrWhiteSpace(r.Text)))
        {
            return null;
        }

        var (passages, searchFailed) = await search.FindPassagesAsync(
            new SearchRequest(EmbeddingIndexer.RequestText(letter.Request.Subject, letter.Request.Content))
            {
                KnowledgeKinds = LegalKinds,
                Limit = LegalPassages,
            },
            ct);
        var attempt = await ai.GetStructuredAsync<DraftAnswer>(
            "draft-response", SystemPrompt(letter), UserPrompt(letter, replies, passages), ct);
        if (attempt.Failed || searchFailed)
        {
            warnings.Add(AiGateway.FallbackWarning);
        }

        var body = attempt.Value?.Body is { } text ? CleanModelBody(text) : "";
        return body.Length == 0
            ? null
            : new ModelAnswer(body, ConfidentAddressee(attempt.Value!) ?? letter.Addressee);
    }

    private static Addressee? ConfidentAddressee(DraftAnswer answer) =>
        Enum.TryParse<Addressee>(answer.Addressee?.Trim(), ignoreCase: true, out var parsed)
        && Enum.IsDefined(parsed)
        && !char.IsDigit(answer.Addressee!.Trim()[0])
        && answer.AddresseeConfidence is >= AddresseeOverrideConfidence
            ? parsed
            : null;

    private string Letter(RequestFacts request, Addressee addressee, string body)
    {
        var parts = new List<string> { LetterLayout.Header(Organization, clock.Today, request.CaseNumber) };
        if (PostalAddressee(request) is { } block)
        {
            parts.Add(block);
        }

        parts.Add(LetterLayout.Salutation(addressee));
        parts.Add(body);
        parts.Add(LetterLayout.Closing(Organization));
        parts.Add(LetterLayout.Footer(Organization));
        return string.Join("\n\n", parts);
    }

    private static string? PostalAddressee(RequestFacts request)
    {
        if (request.ReplyChannel != ReplyChannel.Post)
        {
            return null;
        }

        var contact = request.RequesterContact is { } value && !value.Contains('@')
            ? PostalCodeSeparator().Replace(value.Replace("\r\n", "\n").Trim(), "\n")
            : null;
        var lines = new[] { request.RequesterName?.Trim(), contact }.Where(l => !string.IsNullOrEmpty(l)).ToList();
        return lines.Count == 0 ? null : string.Join("\n", lines);
    }

    private string Opening(LetterFacts letter, string subjectClause, bool withSources = true)
    {
        var sources = !withSources || letter.Departments.Count == 0
            ? ""
            : letter.Departments.Count == 1
                ? $", na podstawie informacji przygotowanych przez komórkę merytoryczną, tj. {Unit(letter.Departments[0])}"
                : $", na podstawie informacji przygotowanych przez jednostki merytoryczne, tj. {JoinUnits(letter.Departments)}";
        return $"w odpowiedzi na wystosowany {LetterLayout.ByYou(letter.Addressee)} wniosek o udostępnienie informacji " +
               $"publicznej {subjectClause}{sources}, uprzejmie informuję, że";
    }

    private string TemplateBody(LetterFacts letter, List<DepartmentReply> replies)
    {
        var subject = $"dotyczący: „{SearchText.CollapseWhitespace(letter.Request.Subject)}”";
        if (letter.NotHolder)
        {
            return NotHolderBody(letter, subject, replies.Where(r => !string.IsNullOrWhiteSpace(r.Text)).ToList());
        }

        if (replies.Count == 0)
        {
            return $"{Opening(letter, subject)} [DO UZUPEŁNIENIA: treść odpowiedzi na wniosek].";
        }

        var body = new StringBuilder()
            .Append(Opening(letter, subject))
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

    private string NotHolderBody(LetterFacts letter, string subject, List<DepartmentReply> replies)
    {
        var redirect = replies.Select(r => (Reply: r, Match: Redirect().Match(r.Text))).First(x => x.Match.Success);
        var after = redirect.Reply.Text[(redirect.Match.Index + redirect.Match.Length)..];
        var entity = EntityName().Match(after) is { Success: true } e && e.Value.Trim().Length > 2
            ? e.Value.Trim()
            : "[DO UZUPEŁNIENIA: nazwa właściwego podmiotu]";
        var contacts = ContactLine().Matches(after[..Math.Min(after.Length, 800)])
            .Select(m => m.Value.Trim().TrimEnd('.', ',', ';'))
            .Distinct()
            .ToList();
        var holder = Organization.Name;
        return string.Join("\n\n",
            $"{Opening(letter, subject, withSources: false)} {holder} nie jest dysponentem wnioskowanych danych.",
            "Jednocześnie wskazuję, iż zgodnie z zasadą dostępu do informacji publicznej, wyrażoną w art. 4 ust. 3 " +
            $"{LetterLayout.ActCitation}, obowiązane do udostępnienia informacji publicznej są podmioty publiczne będące " +
            "w posiadaniu tych informacji.",
            $"W związku z powyższym {holder} uprzejmie informuje, iż Wnioskodawca w zakresie wnioskowanych informacji " +
            $"winien się zwrócić do właściwego dysponenta danych, tj. {entity}.",
            "Poniżej zamieszczam dane do kontaktu:",
            contacts.Count > 0
                ? string.Join("\n", contacts.Prepend(entity))
                : "[DO UZUPEŁNIENIA: dane kontaktowe właściwego podmiotu]");
    }

    private string Unit(string departmentName) => $"{departmentName} {OrganizationGenitive()}";

    private string JoinUnits(IReadOnlyList<string> departments) =>
        string.Join(", ", departments.Take(departments.Count - 1).Select(Unit)) + " oraz " + Unit(departments[^1]);

    private string OrganizationGenitive() =>
        Organization.Name.StartsWith("Urząd ", StringComparison.Ordinal)
            ? "Urzędu " + Organization.Name["Urząd ".Length..]
            : Organization.Name;

    private string SystemPrompt(LetterFacts letter) => $"""
        Jesteś asystentem urzędnika ({Organization.Name}, {Organization.UnitName}) i przygotowujesz projekt odpowiedzi
        na wniosek o udostępnienie informacji publicznej. Projekt zostanie sprawdzony, poprawiony i podpisany przez urzędnika.
        Zasady:
        - Korzystaj wyłącznie z faktów zawartych w odpowiedziach komórek merytorycznych (<odpowiedz_wydzialu>). Kontekst prawny (<kontekst_prawny>) służy wyłącznie do prawidłowego powołania przepisów.
        - Nigdy nie wymyślaj liczb, kwot, dat, nazwisk ani nazw. Jeżeli brakuje informacji potrzebnej do odpowiedzi na którąś część wniosku, wstaw w tym miejscu znacznik [DO UZUPEŁNIENIA: opis brakującej informacji].
        - Treść pisma (pole body) zacznij małą literą, bo następuje po zwrocie grzecznościowym, od zdania według wzoru: „{Opening(letter, "dotyczący <przedmiot wniosku w dopełniaczu>")} …”. Następnie odpowiedz na wniosek punkt po punkcie; przy kilku pytaniach stosuj oznaczenia „Ad 1”, „Ad 2” itd.
        - Jeżeli z odpowiedzi komórek wynika, że {Organization.Name} nie posiada wnioskowanych informacji, a ich dysponentem jest inny podmiot, napisz pismo według przykładu 3: powołaj art. 4 ust. 3 {LetterLayout.ActCitation}, wskaż właściwy podmiot i podaj jego dane kontaktowe wyłącznie na podstawie odpowiedzi komórki.
        - Pisz formalną, urzędową polszczyzną, w pierwszej osobie liczby pojedynczej („uprzejmie informuję”), w stylu przykładów.
        - Zwróć czysty tekst bez Markdown (bez gwiazdek, krzyżyków i tabel). Akapity oddzielaj pustą linią.
        - Nie dodawaj nagłówka, miejscowości i daty, znaku sprawy, adresata, zwrotu „Szanowny Panie”, formuły końcowej, podpisu ani stopki — zostaną dodane automatycznie.
        - W polu addressee podaj formę zwracania się do wnioskodawcy: Man (Pan), Woman (Pani) albo Organisation (Państwo), a w polu addresseeConfidence pewność od 0 do 1. Domyślnie przyjmij {letter.Addressee}; zmień formę tylko wtedy, gdy treść wniosku jednoznacznie wskazuje inną (np. żeńskie formy czasowników, podpis, wniosek instytucji). Treść pisma musi używać tej samej formy.
        - {PromptText.UntrustedDataRule}

        Przykłady stylu (fragmenty prawdziwych odpowiedzi, bez nagłówka i podpisu):
        <przyklad numer="1">
        {DraftStyleExamples.Answer}
        </przyklad>
        <przyklad numer="2">
        {DraftStyleExamples.NoData}
        </przyklad>
        <przyklad numer="3">
        {DraftStyleExamples.NotTheHolder}
        </przyklad>
        """;

    private static string UserPrompt(LetterFacts letter, List<DepartmentReply> replies, KnowledgePassage[] passages)
    {
        var request = letter.Request;
        var prompt = new StringBuilder()
            .AppendLine($"Znak sprawy: {request.CaseNumber}")
            .AppendLine($"Data wpływu wniosku: {LetterLayout.LongDate(request.ReceivedOn)}")
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

        if (letter.NotHolder)
        {
            prompt.AppendLine("Uwaga: z odpowiedzi komórki wynika, że dysponentem wnioskowanych informacji jest inny podmiot — zastosuj wzór z przykładu 3.");
        }

        prompt.Append("Przygotuj treść pisma zgodnie z zasadami.");
        return prompt.ToString();
    }

    private sealed record RequestFacts(
        string CaseNumber,
        DateOnly ReceivedOn,
        string? RequesterName,
        string? RequesterContact,
        ReplyChannel ReplyChannel,
        string Subject,
        string Content);

    private sealed record LetterFacts(
        RequestFacts Request, IReadOnlyList<string> Departments, Addressee Addressee, bool NotHolder);

    private sealed record ModelAnswer(string Body, Addressee Addressee);

    private sealed record DepartmentReply(string? DepartmentName, string FileName, string Text)
    {
        public string Label => DepartmentName ?? $"komórkę merytoryczną (plik {FileName})";
    }

    [GeneratedRegex(
        @"(?:właściw\p{L}*\s+(?:podmiotem|dysponentem|organem|jednostką)(?:\s+\p{L}+){0,3}?\s+jest|" +
        @"(?:dysponentem|w\s+posiadaniu)\s+(?:tych\s+|wnioskowanych\s+)?(?:danych|informacji)\s+jest|" +
        @"(?:należy|winien|winna|powinien|powinna|proszę|prosimy)\s+(?:się\s+)?zwrócić\s+(?:się\s+)?do)\s+",
        RegexOptions.IgnoreCase)]
    private static partial Regex Redirect();

    [GeneratedRegex(@"^[^,.;:(\n]{3,200}")]
    private static partial Regex EntityName();

    [GeneratedRegex(
        @"(?:ul\.|al\.|pl\.|os\.)\s*[^,\n;]+|\d{2}-\d{3}[ ]+\p{Lu}[\p{L}-]+|(?:tel\.?|telefon|fax)[:.]?\s*[^\n;]*?\d[\d ()./-]{5,}\d|" +
        @"(?:e-?mail:?\s*)?[\w.+-]+@[\w-]+(?:\.[\w-]+)+|www\.[\w.-]+\w",
        RegexOptions.IgnoreCase)]
    private static partial Regex ContactLine();

    [GeneratedRegex(@",\s*(?=\d{2}-\d{3}\b)")]
    private static partial Regex PostalCodeSeparator();

    [GeneratedRegex(@"przez\s+(?:Pana|Panią|Państwa)\b")]
    private static partial Regex AddresseeForm();

    [GeneratedRegex(@"(?:w\s+odpowiedzi\s+na\s+wystosowan|odpowiadając\s+na\s+wystosowan|w\s+związku\s+z\s+wystosowan)", RegexOptions.IgnoreCase)]
    private static partial Regex BodyOpening();

    [GeneratedRegex(@"^\s*Szanown\p{L}*\s+(?:Pan\p{L}*|Państwo)\s*[,!]?\s*\n", RegexOptions.IgnoreCase)]
    private static partial Regex LeadingSalutation();

    [GeneratedRegex(@"^(?:W\s|Odpowiadając\s|Uprzejmie\s|Informuję\s)")]
    private static partial Regex LowercaseOpening();

    [GeneratedRegex(@"^[ \t]*(?:Z\s+poważaniem|Z\s+wyrazami\s+szacunku|Łączę\s+wyrazy\s+szacunku)\b", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
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
