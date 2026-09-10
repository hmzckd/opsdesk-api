namespace OpsDesk.Application.Auth.Models;

/// <summary>
/// Carries a newly generated raw verification token and its stored hash.
/// </summary>
public sealed record GeneratedEmailVerificationToken(
    string RawToken,
    string TokenHash);
