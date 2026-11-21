using System.Globalization;
using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Infrastructure.Common;

namespace ClerkAssistant.Api.Features.Requests;

public static class RequestLetters
{
    private const string Act = "ustawy z dnia 6 września 2001 r. o dostępie do informacji publicznej";
    private static readonly CultureInfo Polish = CultureInfo.GetCultureInfo("pl-PL");

    public static string LongDate(DateOnly date) => date.ToString("d MMMM yyyy", Polish);

    public static string ShortDate(DateOnly date) => date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

    public static string ExtensionNotice(InfoRequest request, DateOnly extendedDueOn, OrganizationOptions organization, DateOnly today) =>
        $"""
        {organization.City}, dnia {LongDate(today)} r.

        {organization.Name}
        {organization.UnitName}
        {organization.Address}

        Znak sprawy: {request.CaseNumber}

        {request.RequesterName ?? "Wnioskodawca"}

        Zawiadomienie o przedłużeniu terminu udostępnienia informacji publicznej

        Działając na podstawie art. 13 ust. 2 {Act}, uprzejmie zawiadamiam, że wniosek o udostępnienie informacji publicznej, który wpłynął do Urzędu w dniu {LongDate(request.ReceivedOn)} r., dotyczący: „{request.Subject}”, nie może zostać zrealizowany w terminie 14 dni od dnia jego złożenia.

        Powód opóźnienia: {AsSentence(request.ExtensionReason)}

        Informacja zostanie udostępniona w terminie do dnia {LongDate(extendedDueOn)} r., tj. w terminie nieprzekraczającym 2 miesięcy od dnia złożenia wniosku.

        Z poważaniem

        [podpis osoby upoważnionej]
        """;

    public static DepartmentQueryDto DepartmentQuery(
        InfoRequest request, IReadOnlyList<Department> departments, DateOnly replyBy, OrganizationOptions organization)
    {
        var subject = $"Prośba o informacje do wniosku o udostępnienie informacji publicznej – {request.CaseNumber}";
        var recipients = departments.Count == 0
            ? ""
            : "Do: " + string.Join("; ", departments.Select(d => $"{d.Name} ({d.Symbol})")) + "\n\n";
        var content = string.IsNullOrWhiteSpace(request.Content)
            ? "(treść wniosku nie została wprowadzona do rejestru – znajduje się w dokumentach sprawy)"
            : request.Content.Trim();

        var body = recipients + $"""
            Szanowni Państwo,

            w dniu {LongDate(request.ReceivedOn)} r. wpłynął wniosek o udostępnienie informacji publicznej (znak sprawy: {request.CaseNumber}) dotyczący: „{request.Subject}”.

            Uprzejmie proszę o przekazanie informacji niezbędnych do udzielenia odpowiedzi na wniosek, w zakresie spraw prowadzonych przez Państwa komórkę, w terminie do dnia {LongDate(replyBy)} r. Ustawowy termin udzielenia odpowiedzi wnioskodawcy upływa w dniu {LongDate(request.EffectiveDueOn)} r. (art. 13 {Act}).

            Jeżeli Państwa komórka nie dysponuje wnioskowanymi informacjami, proszę o niezwłoczną informację zwrotną, w miarę możliwości ze wskazaniem właściwej komórki lub jednostki.

            Treść wniosku:
            ----------
            {content}
            ----------

            Z wyrazami szacunku
            {organization.UnitName}
            """;

        return new DepartmentQueryDto(subject, body, replyBy);
    }

    private static string AsSentence(string? text)
    {
        var trimmed = text?.Trim() ?? "";
        return trimmed.Length == 0 || ".!?".Contains(trimmed[^1]) ? trimmed : trimmed + ".";
    }
}
