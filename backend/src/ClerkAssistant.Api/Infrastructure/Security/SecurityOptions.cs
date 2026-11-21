namespace ClerkAssistant.Api.Infrastructure.Security;

public sealed class SecurityOptions
{
    public const string Section = "Security";

    public bool? RequireHttps { get; set; }
    public bool TrustForwardedHeaders { get; set; }
}

public sealed record SecuritySettings(bool RequireHttps, bool TrustForwardedHeaders);
