using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Infrastructure.Ai;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ClerkAssistant.Api.Features.Anonymization;

public sealed record KnownPersonalData(string? Name, string? Contact);

public sealed record DetectionOutcome(List<Redaction> Suggestions, bool AiUsed, bool AiFailed);

internal sealed record PersonalDataAnswer(List<PersonalDataItem>? Items);

internal sealed record PersonalDataItem(string? Text, string? Category);

public sealed class RedactionDetector(AiGateway ai)
{
    private const int MaxContentInPrompt = 30_000;
    private const int MaxItemLength = 300;

    private const string SystemPrompt = $"""
        Jesteś asystentem urzędnika, który przygotowuje pismo do udostępnienia w trybie ustawy o dostępie do informacji publicznej.
        Wskaż w dokumencie dane osobowe osób fizycznych (osób prywatnych), które należy zanonimizować ze względu na ochronę prywatności:
        imiona i nazwiska, adresy, numery PESEL, numery dokumentów tożsamości, numery telefonów, adresy e-mail, numery rachunków bankowych,
        daty urodzenia, informacje o zdrowiu, sytuacji majątkowej lub rodzinnej oraz inne dane pozwalające zidentyfikować osobę.
        Nie wskazuj:
        - danych funkcjonariuszy publicznych i pracowników urzędu działających w ramach pełnionej funkcji (np. dyrektora wydziału podpisującego pismo albo osoby upoważnionej do podpisu);
        - nazw firm, instytucji i organizacji ani ich danych;
        - danych urzędu miasta i jego komórek organizacyjnych;
        - nazw ulic, miejsc i obiektów, które nie są adresem konkretnej osoby prywatnej.
        Każdy fragment przepisz dosłownie, znak w znak, tak jak występuje w dokumencie, i przypisz mu jedną kategorię:
        PersonName, Address, Pesel, IdDocument, Phone, Email, BankAccount, Nip, DateOfBirth albo Other.
        Jeżeli dokument nie zawiera takich danych, zwróć pustą listę.
        {PromptText.UntrustedDataRule}
        """;

    public async Task<DetectionOutcome> DetectAsync(string content, KnownPersonalData? known, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return new DetectionOutcome([], false, false);
        }

        var candidates = PersonalDataRules.Detect(content);
        candidates.AddRange(KnownValues(content, known));

        var answer = await ai.GetStructuredAsync<PersonalDataAnswer>(
            "detect-personal-data", SystemPrompt, PromptText.Wrap("dokument", content, MaxContentInPrompt), ct);
        if (answer.Value is { } value)
        {
            candidates.AddRange(Locate(content, value));
        }

        return new DetectionOutcome(RedactionSet.Merge(candidates), answer.Succeeded, answer.Failed);
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

public static class AnonymizationRegistration
{
    public static IServiceCollection AddAnonymization(this IServiceCollection services)
    {
        services.TryAddSingleton<AiGateway>();
        services.TryAddSingleton<RedactionDetector>();
        return services;
    }
}
