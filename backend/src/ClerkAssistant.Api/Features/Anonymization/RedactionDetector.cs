using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Infrastructure.Ai;

namespace ClerkAssistant.Api.Features.Anonymization;

public sealed record KnownPersonalData(string? Name, string? Contact);

public sealed record DetectionOutcome(List<Redaction> Suggestions, string[] Warnings, bool AiUsed);

internal sealed record PersonalDataAnswer(List<PersonalDataItem>? Items);

internal sealed record PersonalDataItem(string? Text, string? Category);

public sealed class RedactionDetector(AiGateway ai, PublicContactFilter publicContacts)
{
    public const int AiWindowLength = 30_000;
    public const int MaxAiWindows = 3;
    public const string PartialAnalysisWarning =
        "Dokument jest długi — model AI przeanalizował tylko jego początkową część; dalszy tekst sprawdzono wyłącznie regułami.";

    private const int MaxItemLength = 300;

    private const string SystemPrompt = $"""
        Jesteś asystentem urzędnika, który przygotowuje dokument do udostępnienia w trybie ustawy o dostępie do informacji publicznej.
        Wskaż w dokumencie wszystkie dane, które podlegają anonimizacji, i przypisz każdemu fragmentowi jedną kategorię
        (użyj dokładnie podanej nazwy kategorii):
        - PersonName — imię i nazwisko osoby fizycznej niepełniącej funkcji publicznej, także jej podpis lub parafka;
        - Pesel — numer PESEL;
        - IdDocument — seria i numer dowodu osobistego, paszportu lub innego dokumentu tożsamości;
        - Nip — NIP osoby fizycznej nieprowadzącej działalności gospodarczej;
        - DateOfBirth — data urodzenia oraz miejsce urodzenia;
        - Address — adres zamieszkania, zameldowania lub adres korespondencyjny osoby fizycznej;
        - Phone — prywatny numer telefonu;
        - Email — prywatny adres poczty elektronicznej;
        - BankAccount — numer rachunku bankowego osoby fizycznej;
        - FamilySituation — stan cywilny i sytuacja rodzinna (np. liczba dzieci, imiona rodziców);
        - Health — stan zdrowia, niepełnosprawność, nałogi;
        - Financial — sytuacja majątkowa, dochody, zadłużenie, korzystanie ze świadczeń pomocy społecznej;
        - SpecialCategory — poglądy polityczne, przekonania religijne lub światopoglądowe, przynależność związkowa lub partyjna, pochodzenie rasowe lub etniczne, orientacja seksualna;
        - CriminalRecord — karalność, wyroki skazujące, naruszenia prawa;
        - PropertyRecord — numer księgi wieczystej lub numer działki ewidencyjnej, jeżeli pozwala ustalić prywatnego właściciela;
        - VehicleRegistration — numer rejestracyjny prywatnego pojazdu;
        - ProtectedSecret — tajemnica przedsiębiorstwa (dane techniczne, technologiczne, organizacyjne lub handlowe firmy), tajemnica skarbowa (indywidualne dane podatników), tajemnica bankowa i ubezpieczeniowa, tajemnica zawodowa (np. lekarska, adwokacka) oraz informacje niejawne;
        - Other — inne dane pozwalające zidentyfikować osobę prywatną.
        Nie wskazuj:
        - danych funkcjonariuszy publicznych i pracowników urzędu działających w ramach pełnionej funkcji (np. dyrektora wydziału podpisującego pismo, prezydenta miasta, radnego);
        - nazw firm, instytucji i organizacji;
        - danych kontaktowych urzędów i instytucji publicznych (adresy siedzib, numery telefonów i faksów, adresy e-mail w domenach urzędowych, strony internetowe);
        - nazw ulic, miejsc i obiektów, które nie są adresem konkretnej osoby prywatnej.
        Każdy fragment przepisz dosłownie, znak w znak, tak jak występuje w dokumencie. Jeżeli dokument nie zawiera takich danych, zwróć pustą listę.
        {PromptText.UntrustedDataRule}
        """;

    public async Task<DetectionOutcome> DetectAsync(string content, KnownPersonalData? known, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return new DetectionOutcome([], [], false);
        }

        var candidates = PersonalDataRules.Detect(content);
        candidates.AddRange(KnownValues(content, known));

        var warnings = new List<string>();
        var aiUsed = false;
        foreach (var window in AiWindows(content))
        {
            var answer = await ai.GetStructuredAsync<PersonalDataAnswer>(
                "detect-personal-data", SystemPrompt, PromptText.Wrap("dokument", window, AiWindowLength), ct);
            if (answer.Failed)
            {
                warnings.Add(AiGateway.FallbackWarning);
                break;
            }

            if (answer.Value is { } value)
            {
                aiUsed = true;
                candidates.AddRange(Locate(content, value));
            }
        }

        if (aiUsed && content.Length > AiWindowLength * MaxAiWindows)
        {
            warnings.Add(PartialAnalysisWarning);
        }

        return new DetectionOutcome(
            RedactionSet.Merge(candidates.Where(c => !publicContacts.IsPublic(c))), warnings.ToArray(), aiUsed);
    }

    private IEnumerable<string> AiWindows(string content)
    {
        if (!ai.IsEnabled)
        {
            yield break;
        }

        var start = 0;
        for (var window = 0; window < MaxAiWindows && start < content.Length; window++)
        {
            var length = Math.Min(AiWindowLength, content.Length - start);
            if (start + length < content.Length)
            {
                var paragraph = content.LastIndexOf('\n', start + length - 1, length);
                length = paragraph > start + length / 2 ? paragraph - start + 1 : length;
            }

            yield return content.Substring(start, length);
            start += length;
        }
    }

    private static IEnumerable<Redaction> KnownValues(string content, KnownPersonalData? known)
    {
        if (!string.IsNullOrWhiteSpace(known?.Name) && known.Name.Trim().Length >= 3)
        {
            foreach (var r in PersonalDataRules.FindOccurrences(
                         content, known.Name.Trim(), RedactionCategory.PersonName, RedactionSource.Rule))
            {
                yield return r;
            }
        }

        if (!string.IsNullOrWhiteSpace(known?.Contact) && known.Contact.Trim().Length >= 5)
        {
            var contact = known.Contact.Trim();
            var category = contact.Contains('@') ? RedactionCategory.Email : RedactionCategory.Address;
            foreach (var r in PersonalDataRules.FindOccurrences(content, contact, category, RedactionSource.Rule))
            {
                yield return r;
            }
        }
    }

    private static IEnumerable<Redaction> Locate(string content, PersonalDataAnswer answer) =>
        (answer.Items ?? [])
            .Where(i => i?.Text is { Length: >= 2 and <= MaxItemLength } && !string.IsNullOrWhiteSpace(i.Text))
            .DistinctBy(i => i.Text)
            .SelectMany(i => PersonalDataRules.FindOccurrences(
                content, i.Text!.Trim(), ParseCategory(i.Category), RedactionSource.Ai));

    private static RedactionCategory ParseCategory(string? category) =>
        category is not null
        && !char.IsDigit(category.FirstOrDefault())
        && Enum.TryParse<RedactionCategory>(category.Trim(), ignoreCase: true, out var parsed)
        && Enum.IsDefined(parsed)
            ? parsed
            : RedactionCategory.Other;
}
