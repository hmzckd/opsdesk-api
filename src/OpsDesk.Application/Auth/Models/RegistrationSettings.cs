namespace OpsDesk.Application.Auth.Models;

public sealed class RegistrationSettings
{
    public const string SectionName = "Registration";

    public bool PublicRegistrationEnabled { get; set; }
}
