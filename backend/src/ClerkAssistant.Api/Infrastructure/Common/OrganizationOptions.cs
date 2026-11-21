namespace ClerkAssistant.Api.Infrastructure.Common;

public sealed class OrganizationOptions
{
    public const string Section = "Organization";

    public string Name { get; set; } = "Urząd Miasta Bydgoszczy";
    public string UnitName { get; set; } = "Biuro Komunikacji Społecznej";
    public string City { get; set; } = "Bydgoszcz";
    public string Address { get; set; } = "ul. Jezuicka 1, 85-102 Bydgoszcz";
    public string CaseNumberPrefix { get; set; } = "BKS.1431";
}
