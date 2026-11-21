using System.Text.RegularExpressions;
using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Features.Search;
using ClerkAssistant.Api.Infrastructure.Common;
using Microsoft.Extensions.Options;

namespace ClerkAssistant.Api.Features.Anonymization;

public sealed partial class PublicContactFilter
{
    private static readonly string[] PublicSectorDomains = ["gov.pl"];

    private readonly string[] _domains;
    private readonly HashSet<string> _phones;
    private readonly string _address;
    private readonly string _street;

    public PublicContactFilter(IOptions<OrganizationOptions> options)
    {
        var organization = options.Value;
        _domains = PublicSectorDomains
            .Append(DomainOf(organization.Email))
            .Append(organization.Website.Trim().ToLowerInvariant().Replace("https://", "").Replace("http://", "").Replace("www.", "").TrimEnd('/'))
            .Where(d => d.Contains('.'))
            .Distinct()
            .ToArray();
        _phones = PhoneNumber().Matches(organization.Phone).Select(m => PhoneKey(m.Value)).ToHashSet();
        _address = Normalize(organization.Address);
        _street = Normalize(organization.Address.Split(',')[0]);
    }

    public bool IsPublic(Redaction redaction) => redaction.Category switch
    {
        RedactionCategory.Email => IsPublicEmail(redaction.Text),
        RedactionCategory.Phone => _phones.Contains(PhoneKey(redaction.Text)),
        RedactionCategory.Address => IsOrganizationAddress(redaction.Text),
        _ => false,
    };

    public bool IsPublicEmail(string email)
    {
        var domain = DomainOf(email);
        return domain.StartsWith("bip.", StringComparison.Ordinal)
               || _domains.Any(d => domain == d || domain.EndsWith("." + d, StringComparison.Ordinal));
    }

    private bool IsOrganizationAddress(string text)
    {
        var parts = text.Split(',', '\n').Select(Normalize).Where(p => p.Length > 0).ToList();
        return _street.Length > 0
               && parts.Contains(_street)
               && parts.All(p => _address.Contains(p, StringComparison.Ordinal));
    }

    private static string DomainOf(string email) =>
        email[(email.LastIndexOf('@') + 1)..].Trim().TrimEnd('.').ToLowerInvariant();

    private static string PhoneKey(string phone)
    {
        var digits = new string(phone.Where(char.IsAsciiDigit).ToArray());
        return digits.Length > 9 ? digits[^9..] : digits;
    }

    private static string Normalize(string text) =>
        SearchText.CollapseWhitespace(text).Trim(' ', '.', ',', ';').ToLowerInvariant();

    [GeneratedRegex(@"\(?\d[\d ()./-]{6,}\d")]
    private static partial Regex PhoneNumber();
}
