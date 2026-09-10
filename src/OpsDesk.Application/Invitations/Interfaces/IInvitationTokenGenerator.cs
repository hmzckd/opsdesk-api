using OpsDesk.Application.Invitations.Models;

namespace OpsDesk.Application.Invitations.Interfaces;

public interface IInvitationTokenGenerator
{
    // Generates a purpose-specific token and its storage hash.
    GeneratedInvitationToken GenerateToken();

    // Rejects malformed or wrong-purpose tokens before computing their lookup hash.
    string ComputeHash(string rawToken);
}
