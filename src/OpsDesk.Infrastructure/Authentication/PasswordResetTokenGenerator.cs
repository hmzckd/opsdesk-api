using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Application.Auth.Models;

namespace OpsDesk.Infrastructure.Authentication;

public sealed class PasswordResetTokenGenerator(IEmailVerificationTokenGenerator generator)
    : IPasswordResetTokenGenerator
{
    // Separates password-reset credentials from invitations and verification tokens.
    public GeneratedPasswordResetToken GenerateToken()
    {
        string rawToken = "pwd_" + generator.GenerateToken().RawToken;
        return new GeneratedPasswordResetToken(rawToken, ComputeHash(rawToken));
    }

    // Only password-reset credentials are accepted; JWT, verification and invitation tokens are rejected.
    public string ComputeHash(string rawToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken) || rawToken.Length != 47
            || !rawToken.StartsWith("pwd_", StringComparison.Ordinal)
            || rawToken[4..].Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('_' or '-')))
            throw new ArgumentException("Password reset token is invalid or expired.");
        return generator.ComputeHash(rawToken);
    }
}
