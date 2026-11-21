namespace ClerkAssistant.Api.Infrastructure.Common;

public sealed class OrganizationOptions
{
    public const string Section = "Organization";

    public string Name { get; set; } = "Urząd Miasta Bydgoszczy";
    public string UnitName { get; set; } = "Biuro Komunikacji Społecznej";
    public string City { get; set; } = "Bydgoszcz";
    public string Address { get; set; } = "ul. Jezuicka 1, 85-102 Bydgoszcz";
    public string CaseNumberPrefix { get; set; } = "BKS.IV.1431";
    public string Phone { get; set; } = "tel. (52) 58 58 365, fax (52) 58 58 623";
    public string Email { get; set; } = "bks@um.bydgoszcz.pl";
    public string Website { get; set; } = "www.bydgoszcz.pl";
    public string LetterClosing { get; set; } = "Łączę wyrazy szacunku,";
}
