namespace OpsDesk.Application.Auth.Models;

/// <summary>
/// Carries the data required to deliver one verification email.
/// </summary>
public sealed record EmailVerificationEmail(
    string RecipientEmail,
    string RawToken,
    DateTime ExpiresAtUtc);
