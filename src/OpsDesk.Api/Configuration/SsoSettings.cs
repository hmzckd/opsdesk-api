namespace OpsDesk.Api.Configuration;

public sealed class SsoSettings
{
    public const string SectionName = "Sso";

    public bool Enabled { get; set; }

    public string Authority { get; set; } = string.Empty;

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public string PublicOrigin { get; set; } = string.Empty;

    public string[] AllowedEmailDomains { get; set; } = [];
}
