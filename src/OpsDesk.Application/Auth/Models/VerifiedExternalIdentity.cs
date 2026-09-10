namespace OpsDesk.Application.Auth.Models;

// Only the validated OIDC handler constructs this input in production, never a public JSON endpoint.
public sealed record VerifiedExternalIdentity(
    string Issuer, string Subject, string Email, bool EmailVerified, string FirstName, string LastName);
