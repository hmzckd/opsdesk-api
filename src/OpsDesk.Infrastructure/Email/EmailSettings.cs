namespace OpsDesk.Infrastructure.Email;

public sealed class EmailSettings
{
    public const string SectionName = "Email";

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; }

    public bool UseSsl { get; set; }

    public string FromAddress { get; set; } = string.Empty;

    public string FromName { get; set; } = string.Empty;

    public string VerificationUrl { get; set; } = string.Empty;
}
