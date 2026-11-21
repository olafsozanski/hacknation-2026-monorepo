namespace ClerkAssistant.Api.Infrastructure.Seeding;

public sealed class SeedOptions
{
    public const string Section = "Seed";

    public string AdminUserName { get; set; } = "admin";
    public string? AdminPassword { get; set; }
    public bool DemoData { get; set; }
    public string? DemoPassword { get; set; }
}
