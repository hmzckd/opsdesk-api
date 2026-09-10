namespace OpsDesk.Application.Auth.Models;

public sealed record ExternalSignInPolicy(bool Enabled, string Issuer, IReadOnlyCollection<string> AllowedEmailDomains);
