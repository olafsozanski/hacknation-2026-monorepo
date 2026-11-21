using System.Globalization;
using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Infrastructure.Common;
using ClerkAssistant.Api.Infrastructure.Letters;

namespace ClerkAssistant.Api.Features.Requests;

public static class RequestLetters
{
    private const string Paragraph = "\n\n";

    public static string ShortDate(DateOnly date) => date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

    public static string ExtensionNotice(InfoRequest request, DateOnly extendedDueOn, OrganizationOptions organization, DateOnly letterDate)
    {
        var addressee = LetterLayout.GuessAddressee(request.RequesterName);
        var body = request.ExtensionKind == ExtensionKind.Clarification
            ? ClarificationBody(request, addressee, extendedDueOn)
            : QueryBody(request, addressee, extendedDueOn);
        return string.Join(Paragraph,
            LetterLayout.Header(organization, letterDate, request.CaseNumber),
            body,
            LetterLayout.Closing(organization),
            LetterLayout.Footer(organization));
    }

    public static string DepartmentQuerySubject(InfoRequest request, DateOnly replyBy) =>
        $"Wniosek o udostępnienie informacji publicznej {request.CaseNumber} – prośba o informacje do {LetterLayout.LongDate(replyBy)}";

    public static string DepartmentQueryBody(
        InfoRequest request, IReadOnlyList<Department> departments, DateOnly replyBy, OrganizationOptions organization)
    {
        var recipients = departments.Count == 0
            ? ""
            : "Do: " + string.Join("; ", departments.Select(d => $"{d.Name} ({d.Symbol})")) + Paragraph;
        var content = string.IsNullOrWhiteSpace(request.Content)
            ? "(treść wniosku nie została wprowadzona do rejestru – znajduje się w dokumentach sprawy)"
            : request.Content.Trim();

        return recipients + $"""
            Szanowni Państwo,

            w dniu {LetterLayout.LongDate(request.ReceivedOn)} wpłynął wniosek o udostępnienie informacji publicznej (znak sprawy: {request.CaseNumber}) dotyczący: „{request.Subject}”.

            Uprzejmie proszę o przekazanie informacji niezbędnych do udzielenia odpowiedzi na wniosek, w zakresie spraw prowadzonych przez Państwa komórkę, w terminie do dnia {LetterLayout.LongDate(replyBy)}. Ustawowy termin udzielenia odpowiedzi wnioskodawcy upływa w dniu {LetterLayout.LongDate(request.EffectiveDueOn)} (art. 13 {LetterLayout.ActCitation}).

            Jeżeli Państwa komórka nie dysponuje wnioskowanymi informacjami, proszę o niezwłoczną informację zwrotną, w miarę możliwości ze wskazaniem właściwej komórki lub jednostki.

            Treść wniosku:
            ----------
            {content}
            ----------

            {LetterLayout.Closing(organization)}
            """;
    }

    private static string QueryBody(InfoRequest request, Addressee addressee, DateOnly extendedDueOn) =>
        LetterLayout.Salutation(addressee) + Paragraph
        + $"w odpowiedzi na wystosowany {LetterLayout.ByYou(addressee)} wniosek o udostępnienie informacji publicznej dotyczący: „{request.Subject}”, "
        + $"ze względu na {AsClause(request.ExtensionReason)} powiadamiam, że ostateczna odpowiedź nastąpi w terminie późniejszym wskazanym "
        + $"w art. 13 ust. 2 {LetterLayout.ActCitation} – termin udzielenia odpowiedzi: {LetterLayout.LongDate(extendedDueOn)}";

    private static string ClarificationBody(InfoRequest request, Addressee addressee, DateOnly extendedDueOn) =>
        string.Join(Paragraph,
            "WEZWANIE DO DOPRECYZOWANIA\nZAKRESU OTRZYMANEGO WNIOSKU",
            LetterLayout.Salutation(addressee),
            $"w związku z wystosowanym {LetterLayout.ByYou(addressee)} wnioskiem o udostępnienie informacji publicznej dotyczącym: „{request.Subject}”, "
            + "uprzejmie proszę o doprecyzowanie zakresu wniosku.",
            AsSentence(request.ExtensionReason),
            "Proszę o przesłanie doprecyzowania w terminie 7 dni od otrzymania niniejszego pisma. Po upływie tego terminu wniosek zostanie "
            + "pozostawiony bez rozpoznania, o czym Wnioskodawca zostanie powiadomiony odrębnym pismem.",
            $"Jednocześnie stosownie do treści art. 13 ust. 2 {LetterLayout.ActCitation} powiadamiam, że wyznaczam nowy termin na rozpatrzenie "
            + $"wniosku, wskazując dzień {LetterLayout.LongDate(extendedDueOn)}");

    private static string AsClause(string? text)
    {
        var clause = (text ?? "").Trim().TrimEnd('.', ' ');
        return clause.Length > 1 && char.IsUpper(clause[0]) && !char.IsUpper(clause[1])
            ? char.ToLowerInvariant(clause[0]) + clause[1..]
            : clause;
    }

    private static string AsSentence(string? text)
    {
        var sentence = (text ?? "").Trim();
        if (sentence.Length == 0)
        {
            return sentence;
        }

        sentence = char.ToUpperInvariant(sentence[0]) + sentence[1..];
        return ".!?".Contains(sentence[^1]) ? sentence : sentence + ".";
    }
}
