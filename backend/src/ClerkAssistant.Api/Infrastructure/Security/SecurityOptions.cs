namespace ClerkAssistant.Api.Infrastructure.Security;

public sealed class SecurityOptions
{
    public const string Section = "Security";

    public bool? RequireHttps { get; set; }
    public bool TrustForwardedHeaders { get; set; }
    public string[] KnownProxies { get; set; } = [];
    public int MaxSessionHours { get; set; } = 12;
    public string? DataProtectionCertificatePath { get; set; }
    public string? DataProtectionCertificatePassword { get; set; }
}

public sealed record SecuritySettings(bool RequireHttps, bool TrustForwardedHeaders);
