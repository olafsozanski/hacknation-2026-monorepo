using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Features.Requests;
using ClerkAssistant.Api.Infrastructure.Common;
using ClerkAssistant.Api.Infrastructure.Letters;

namespace ClerkAssistant.Api.Tests.Register;

public class RequestLettersTests
{
    private static readonly OrganizationOptions Organization = new();

    private const string Header543 = "URZĄD MIASTA BYDGOSZCZY\nBiuro Komunikacji Społecznej\n\nBydgoszcz, dnia 28 sierpnia 2026 r.\n\nBKS.IV.1431.543.2026";

    private static readonly string ClosingAndFooter =
        "Łączę wyrazy szacunku,\nBiuro Komunikacji Społecznej\nUrząd Miasta Bydgoszczy\n\n"
        + "ul. Jezuicka 1, 85-102 Bydgoszcz\ntel. (52) 58 58 365, fax (52) 58 58 623\ne-mail: bks@um.bydgoszcz.pl, www.bydgoszcz.pl";

    [Fact]
    public void Query_notice_follows_the_kwerenda_letter()
    {
        var request = Request("Stowarzyszenie Obrony Zwierzątek", ExtensionKind.Query,
            "Konieczność przeprowadzenia całościowej kwerendy w zakresie wnioskowanych danych.");

        var notice = RequestLetters.ExtensionNotice(request, new DateOnly(2026, 9, 25), Organization, new DateOnly(2026, 8, 28));

        Assert.Equal(
            Header543 + "\n\nSzanowni Państwo,\n\n"
            + "w odpowiedzi na wystosowany przez Państwa wniosek o udostępnienie informacji publicznej dotyczący: "
            + "„Opieka nad bezdomnymi zwierzętami i ich wyłapywanie w 2025 r.”, ze względu na konieczność przeprowadzenia całościowej "
            + "kwerendy w zakresie wnioskowanych danych powiadamiam, że ostateczna odpowiedź nastąpi w terminie późniejszym wskazanym "
            + $"w art. 13 ust. 2 {LetterLayout.ActCitation} – termin udzielenia odpowiedzi: 25 września 2026 r.\n\n"
            + ClosingAndFooter,
            notice);
    }

    [Fact]
    public void Clarification_notice_follows_the_call_to_clarify_letter()
    {
        var request = Request("Ewa Górna", ExtensionKind.Clarification, "wskazanie, o jakie dokładnie akta sprawy chodzi");

        var notice = RequestLetters.ExtensionNotice(request, new DateOnly(2026, 9, 18), Organization, new DateOnly(2026, 8, 28));

        Assert.Equal(
            Header543 + "\n\nWEZWANIE DO DOPRECYZOWANIA\nZAKRESU OTRZYMANEGO WNIOSKU\n\nSzanowna Pani,\n\n"
            + "w związku z wystosowanym przez Panią wnioskiem o udostępnienie informacji publicznej dotyczącym: "
            + "„Opieka nad bezdomnymi zwierzętami i ich wyłapywanie w 2025 r.”, uprzejmie proszę o doprecyzowanie zakresu wniosku.\n\n"
            + "Wskazanie, o jakie dokładnie akta sprawy chodzi.\n\n"
            + "Proszę o przesłanie doprecyzowania w terminie 7 dni od otrzymania niniejszego pisma. Po upływie tego terminu wniosek "
            + "zostanie pozostawiony bez rozpoznania, o czym Wnioskodawca zostanie powiadomiony odrębnym pismem.\n\n"
            + $"Jednocześnie stosownie do treści art. 13 ust. 2 {LetterLayout.ActCitation} powiadamiam, że wyznaczam nowy termin "
            + "na rozpatrzenie wniosku, wskazując dzień 18 września 2026 r.\n\n"
            + ClosingAndFooter,
            notice);
    }

    [Fact]
    public void Notice_addresses_a_man_and_keeps_acronyms_in_the_reason()
    {
        var request = Request("Jacek Rzeźbiarz", ExtensionKind.Query, "ZDMiKP musi przekazać dane");

        var notice = RequestLetters.ExtensionNotice(request, new DateOnly(2026, 9, 25), Organization, new DateOnly(2026, 8, 28));

        Assert.Contains("Szanowny Panie,\n\nw odpowiedzi na wystosowany przez Pana wniosek", notice);
        Assert.Contains("ze względu na ZDMiKP musi przekazać dane powiadamiam", notice);
    }

    [Fact]
    public void Missing_extension_kind_defaults_to_query_letter()
    {
        var request = Request(null, null, "Konieczność kwerendy archiwalnej.");

        var notice = RequestLetters.ExtensionNotice(request, new DateOnly(2026, 9, 25), Organization, new DateOnly(2026, 8, 28));

        Assert.Contains("Szanowni Państwo,", notice);
        Assert.Contains("ze względu na konieczność kwerendy archiwalnej powiadamiam", notice);
        Assert.DoesNotContain("WEZWANIE", notice);
    }

    private static InfoRequest Request(string? requesterName, ExtensionKind? kind, string reason) => new()
    {
        CaseNumber = "BKS.IV.1431.543.2026",
        Subject = "Opieka nad bezdomnymi zwierzętami i ich wyłapywanie w 2025 r.",
        RequesterName = requesterName,
        ReceivedOn = new DateOnly(2026, 8, 14),
        DueOn = new DateOnly(2026, 8, 28),
        ExtensionKind = kind,
        ExtensionReason = reason,
    };
}
