namespace ClerkAssistant.Api.Tests.Ai;

public sealed class DemoAiFactory : AiApiFactory
{
    public DemoAiFactory() => Clock.Now = new DateTimeOffset(2026, 11, 21, 8, 0, 0, TimeSpan.FromHours(1));

    protected override IReadOnlyDictionary<string, string?> Settings => new Dictionary<string, string?>
    {
        ["Seed:DemoData"] = "true",
        ["Seed:DemoPassword"] = "Urzednik12345",
    };
}
