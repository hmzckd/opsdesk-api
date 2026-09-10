namespace OpsDesk.Application.Auth.Models;

public sealed record PasswordResetEmail(string RecipientEmail, string RawToken, DateTime ExpiresAtUtc);
