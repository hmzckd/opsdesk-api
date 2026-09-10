using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Application.Invitations.Interfaces;
using OpsDesk.Application.Invitations.Models;

namespace OpsDesk.Infrastructure.Authentication;

public sealed class InvitationTokenGenerator(IEmailVerificationTokenGenerator generator)
    : IInvitationTokenGenerator
{
    // Reuses the cryptographic generator while separating invitation tokens by purpose.
    public GeneratedInvitationToken GenerateToken()
    {
        string rawToken = "inv_" + generator.GenerateToken().RawToken;
        return new GeneratedInvitationToken(rawToken, ComputeHash(rawToken));
    }

    // Accepts only the URL-safe invitation format; verification tokens have no inv_ prefix.
    public string ComputeHash(string rawToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken) || rawToken.Length != 47
            || !rawToken.StartsWith("inv_", StringComparison.Ordinal)
            || rawToken[4..].Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character is not ('_' or '-')))
        {
            throw new ArgumentException("Invitation token is invalid or expired.");
        }

        return generator.ComputeHash(rawToken);
    }
}
