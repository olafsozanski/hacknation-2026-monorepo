using System.Globalization;
using System.Text.RegularExpressions;
using ClerkAssistant.Api.Infrastructure.Common;

namespace ClerkAssistant.Api.Infrastructure.Letters;

public enum Addressee
{
    Man,
    Woman,
    Organisation,
}

public static partial class LetterLayout
{
    public const string ActCitation =
        "ustawy z dnia 6 września 2001 r. o dostępie do informacji publicznej (Dz. U. z 2022 r. poz. 902, z 2025 r. poz. 1844)";

    private static readonly CultureInfo Polish = CultureInfo.GetCultureInfo("pl-PL");

    private static readonly string[] OrganisationMarkers =
    [
        "stowarzyszenie", "fundacja", "spółka", "sp. z o.o", "s.a.", "redakcja", "parafia", "związek", "fundacji",
        "urząd", "instytut", "kancelaria", "rada", "klub", "towarzystwo", "partia", "komitet", "biuro", "grupa",
    ];

    private static readonly HashSet<string> MaleNamesEndingWithA = new(StringComparer.OrdinalIgnoreCase)
    {
        "Kuba", "Barnaba", "Bonawentura", "Kosma", "Jarema", "Dyzma",
    };

    public static string LongDate(DateOnly date) => date.ToString("d MMMM yyyy", Polish) + " r.";

    public static string PlaceAndDate(OrganizationOptions organization, DateOnly date) =>
        $"{organization.City}, dnia {LongDate(date)}";

    public static string Header(OrganizationOptions organization, DateOnly date, string caseNumber) =>
        $"{organization.Name.ToUpper(Polish)}\n{organization.UnitName}\n\n{PlaceAndDate(organization, date)}\n\n{caseNumber}";

    public static string Closing(OrganizationOptions organization) =>
        $"{organization.LetterClosing}\n{organization.UnitName}\n{organization.Name}";

    public static string Footer(OrganizationOptions organization) =>
        $"{organization.Address}\n{organization.Phone}\ne-mail: {organization.Email}, {organization.Website}";

    public static Addressee GuessAddressee(string? requesterName)
    {
        var name = requesterName?.Trim();
        if (string.IsNullOrEmpty(name) || name.Contains('@'))
        {
            return Addressee.Organisation;
        }

        var lower = name.ToLower(Polish);
        if (OrganisationMarkers.Any(lower.Contains) || name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > 3)
        {
            return Addressee.Organisation;
        }

        var firstName = FirstWord().Match(name).Value;
        if (firstName.Length < 2)
        {
            return Addressee.Organisation;
        }

        return firstName.EndsWith('a') && !MaleNamesEndingWithA.Contains(firstName) ? Addressee.Woman : Addressee.Man;
    }

    public static string Salutation(Addressee addressee) => addressee switch
    {
        Addressee.Man => "Szanowny Panie,",
        Addressee.Woman => "Szanowna Pani,",
        _ => "Szanowni Państwo,",
    };

    public static string ByYou(Addressee addressee) => addressee switch
    {
        Addressee.Man => "przez Pana",
        Addressee.Woman => "przez Panią",
        _ => "przez Państwa",
    };

    [GeneratedRegex(@"^\p{L}+")]
    private static partial Regex FirstWord();
}
