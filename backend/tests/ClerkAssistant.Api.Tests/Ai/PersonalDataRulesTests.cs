using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Features.Anonymization;
using ClerkAssistant.Api.Infrastructure.Ai;
using ClerkAssistant.Api.Infrastructure.Common;
using ClerkAssistant.Api.Infrastructure.Letters;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ClerkAssistant.Api.Tests.Ai;

public class PersonalDataRulesTests
{
    [Theory]
    [InlineData("44051401359", true)]
    [InlineData("02070803628", true)]
    [InlineData("44051401358", false)]
    [InlineData("4405140135", false)]
    public void Pesel_checksum_is_validated(string value, bool expected) =>
        Assert.Equal(expected, PersonalDataRules.IsValidPesel(value));

    [Theory]
    [InlineData("1234563218", true)]
    [InlineData("123-456-32-18", true)]
    [InlineData("526-025-02-74", true)]
    [InlineData("1234563219", false)]
    public void Nip_checksum_is_validated(string value, bool expected) =>
        Assert.Equal(expected, PersonalDataRules.IsValidNip(value));

    [Theory]
    [InlineData("ABA300000", true)]
    [InlineData("ABA 300000", true)]
    [InlineData("ABS123456", true)]
    [InlineData("ABA300001", false)]
    [InlineData("aba300000", false)]
    public void Id_card_checksum_is_validated(string value, bool expected) =>
        Assert.Equal(expected, PersonalDataRules.IsValidIdCard(value));

    [Theory]
    [InlineData("PL61 1090 1014 0000 0712 1981 2874", true)]
    [InlineData("61109010140000071219812874", true)]
    [InlineData("61109010140000071219812875", false)]
    [InlineData("6110901014000007121981287", false)]
    public void Polish_iban_checksum_is_validated(string value, bool expected) =>
        Assert.Equal(expected, PersonalDataRules.IsValidPolishIban(value));

    [Fact]
    public void Detects_identifiers_with_valid_checksums_only()
    {
        const string text =
            "PESEL 44051401359, błędny PESEL 44051401358, NIP 123-456-32-18, dowód ABA300000, " +
            "konto PL61 1090 1014 0000 0712 1981 2874.";

        var found = PersonalDataRules.Detect(text);

        Assert.Equal(
            [
                (RedactionCategory.Pesel, "44051401359"),
                (RedactionCategory.Nip, "123-456-32-18"),
                (RedactionCategory.IdDocument, "ABA300000"),
                (RedactionCategory.BankAccount, "PL61 1090 1014 0000 0712 1981 2874"),
            ],
            found.Select(r => (r.Category, r.Text)));
        Assert.All(found, r =>
        {
            Assert.Equal(RedactionStatus.Suggested, r.Status);
            Assert.Equal(RedactionSource.Rule, r.Source);
            Assert.Equal(r.Text, text.Substring(r.Start, r.Length));
        });
    }

    [Theory]
    [InlineData("Proszę dzwonić: +48 501 234 567.", "+48 501 234 567")]
    [InlineData("tel. 501-234-567", "501-234-567")]
    [InlineData("tel. 501234567 w godzinach pracy", "501234567")]
    [InlineData("sekretariat 52 58 58 000", "52 58 58 000")]
    [InlineData("tel. (52) 349 12 34", "(52) 349 12 34")]
    public void Detects_phone_numbers(string text, string phone)
    {
        var found = PersonalDataRules.Detect(text);

        Assert.Contains(found, r => r.Category == RedactionCategory.Phone && r.Text == phone);
    }

    [Theory]
    [InlineData("Numer sprawy 1234501234567890 w systemie")]
    [InlineData("Kwota 123 456 789 zł brutto")]
    [InlineData("Znak sprawy BKS.IV.1431.12.2026 z dnia 21.11.2026")]
    public void Ignores_numbers_that_are_not_phones(string text) =>
        Assert.DoesNotContain(PersonalDataRules.Detect(text), r => r.Category == RedactionCategory.Phone);

    [Fact]
    public void Detects_email_and_addresses()
    {
        const string text =
            "Jan Kowalski, ul. Jana Pawła II 12/4, 85-102 Bydgoszcz, e-mail: jan.kowalski@example.com. " +
            "Druga osoba mieszka przy al. Mickiewicza 5 m. 3.";

        var found = PersonalDataRules.Detect(text);

        Assert.Contains(found, r => r.Category == RedactionCategory.Email && r.Text == "jan.kowalski@example.com");
        Assert.Contains(found, r => r.Category == RedactionCategory.Address && r.Text == "ul. Jana Pawła II 12/4, 85-102 Bydgoszcz");
        Assert.Contains(found, r => r.Category == RedactionCategory.Address && r.Text == "al. Mickiewicza 5 m. 3");
    }

    [Fact]
    public void Detects_standalone_postal_code_with_city()
    {
        var found = PersonalDataRules.Detect("Adres do korespondencji: 86-300 Grudziądz");

        Assert.Contains(found, r => r.Category == RedactionCategory.Address && r.Text == "86-300 Grudziądz");
    }

    [Theory]
    [InlineData("Jan Kowalski, ur. 12.03.1985 r., zamieszkały", "12.03.1985")]
    [InlineData("urodzona dnia 5 maja 1990 w Toruniu", "5 maja 1990 w Toruniu")]
    [InlineData("Jan Kowalski, ur. 12.03.1980 r. w Toruniu, zam.", "12.03.1980 r. w Toruniu")]
    [InlineData("urodzony w Nowym Targu dnia 5 maja 1990 r.", "Nowym Targu dnia 5 maja 1990")]
    [InlineData("data urodzenia: 1985-03-12", "1985-03-12")]
    public void Detects_dates_of_birth_only_in_context(string text, string date) =>
        Assert.Contains(PersonalDataRules.Detect(text), r => r.Category == RedactionCategory.DateOfBirth && r.Text == date);

    [Fact]
    public void Does_not_treat_ordinary_dates_as_dates_of_birth() =>
        Assert.DoesNotContain(
            PersonalDataRules.Detect("Wniosek z dnia 12.03.2026 r. wpłynął 13 marca 2026 r."),
            r => r.Category == RedactionCategory.DateOfBirth);

    [Fact]
    public void Detected_spans_never_overlap()
    {
        const string text = "Kontakt: ul. Długa 5, 85-001 Bydgoszcz, tel. 501 234 567, PESEL 44051401359.";

        var found = PersonalDataRules.Detect(text);

        for (var i = 1; i < found.Count; i++)
        {
            Assert.True(found[i - 1].Start + found[i - 1].Length <= found[i].Start);
        }
    }

    [Theory]
    [InlineData("Nieruchomość ma założoną księgę wieczystą nr BY1B/00012345/6.", "BY1B/00012345/6")]
    [InlineData("KW: WA4M / 00123456 / 2 prowadzona przez sąd", "WA4M / 00123456 / 2")]
    public void Detects_land_register_numbers(string text, string number) =>
        Assert.Contains(PersonalDataRules.Detect(text), r => r.Category == RedactionCategory.PropertyRecord && r.Text == number);

    [Theory]
    [InlineData("Właściciel działki nr 123/4 obręb 0045 złożył wniosek.", "123/4")]
    [InlineData("dotyczy dz. nr 12/3 i 12/5 w Fordonie", "12/3 i 12/5")]
    [InlineData("działki ewidencyjnej nr 45 położonej przy", "45")]
    public void Detects_parcel_numbers_in_context(string text, string parcel) =>
        Assert.Contains(PersonalDataRules.Detect(text), r => r.Category == RedactionCategory.PropertyRecord && r.Text == parcel);

    [Theory]
    [InlineData("Pojazd marki Opel o nr rej. CB 12345 parkował", "CB 12345")]
    [InlineData("samochód o numerze rejestracyjnym CBY1234A", "CBY1234A")]
    [InlineData("tablice rejestracyjne: CT 9876A", "CT 9876A")]
    public void Detects_vehicle_plates_in_context(string text, string plate) =>
        Assert.Contains(PersonalDataRules.Detect(text), r => r.Category == RedactionCategory.VehicleRegistration && r.Text == plate);

    [Theory]
    [InlineData("Uchwała obejmuje obszar 123/4 oraz CB 12345 w tabeli.")]
    [InlineData("Ustawa z dnia 21 sierpnia 1997 r. (Dz. U. z 2024 r. poz. 1145) w pkt 12/3.")]
    public void Ignores_parcels_and_plates_without_context(string text) =>
        Assert.DoesNotContain(PersonalDataRules.Detect(text), r =>
            r.Category is RedactionCategory.PropertyRecord or RedactionCategory.VehicleRegistration);

    [Fact]
    public void Detects_private_person_names_in_context_and_all_their_occurrences()
    {
        const string text =
            "Pan Jan Kowalski złożył wniosek. Pismo przekazano Pana Prezydenta Rafała Bruskiego do wiadomości. " +
            "Odpowiedź otrzyma Jan Kowalski.\nWnioskodawczyni: Ewa Nowak-Wiśniewska\n\nZ poważaniem\nAnna Zielińska\n\n" +
            "Łączę wyrazy szacunku,\nBiuro Komunikacji Społecznej\nUrząd Miasta Bydgoszczy";

        var names = PersonalDataRules.Detect(text)
            .Where(r => r.Category == RedactionCategory.PersonName)
            .Select(r => r.Text)
            .ToList();

        Assert.Equal(["Jan Kowalski", "Jan Kowalski", "Ewa Nowak-Wiśniewska", "Anna Zielińska"], names);
    }

    [Theory]
    [InlineData("Dziękuję za odpowiedź.\nPozdrawiam | Ewa Góra", "Ewa Góra", RedactionCategory.PersonName)]
    [InlineData("bulwaru?--\n\nZ wyrazami szacunku\n\nE. Góra", "E. Góra", RedactionCategory.PersonName)]
    [InlineData("Jerzy Dociekliwy\nul. Pod lasem 4\n\nSzanowny Panie,", "ul. Pod lasem 4", RedactionCategory.Address)]
    [InlineData("Jerzy Dociekliwy\nul. Pod lasem 4\n\nSzanowny Panie,", "Jerzy Dociekliwy", RedactionCategory.PersonName)]
    public void Detects_signatures_and_addressee_blocks_from_real_letters(string text, string value, RedactionCategory category) =>
        Assert.Contains(PersonalDataRules.Detect(text), r => r.Category == category && r.Text == value);

    [Fact]
    public async Task Office_contact_data_and_public_sector_addresses_are_not_flagged()
    {
        var organization = new OrganizationOptions();
        var detector = new RedactionDetector(
            new AiGateway(new AiClients(Options.Create(new AiOptions())), NullLogger<AiGateway>.Instance),
            new PublicContactFilter(Options.Create(organization)));
        var content = string.Join("\n\n",
            "Kontakt z wnioskodawcą: jan.kowalski@gmail.com, tel. 501 234 567, ul. Długa 5, 85-001 Bydgoszcz.",
            "Dane kontaktowe podmiotu: bydgoszcz@torun.wkz.gov.pl, zarzad@zdmikp.bydgoszcz.pl, bip@um.bydgoszcz.pl.",
            "85-102 Bydgoszcz, ul. Jezuicka 1\ntel.: (52) 58-58-365, fax.: (52) 58-58-623\ne-mail: bks@um.bydgoszcz.pl,www.bydgoszcz.pl",
            LetterLayout.Footer(organization));

        var outcome = await detector.DetectAsync(content, null, TestContext.Current.CancellationToken);

        Assert.Equal(
            ["jan.kowalski@gmail.com", "501 234 567", "ul. Długa 5, 85-001 Bydgoszcz"],
            outcome.Suggestions.Select(r => r.Text));
        Assert.False(outcome.AiUsed);
        Assert.Empty(outcome.Warnings);
    }
}
