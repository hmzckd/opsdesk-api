using OpsDesk.Application.Auth.Models;

namespace OpsDesk.Application.Auth.Interfaces;

public interface IEmailVerificationTokenGenerator
{
    /// <summary>
    /// Generates a new unpredictable token and its storage-safe hash.
    /// </summary>
    GeneratedEmailVerificationToken GenerateToken();

    /// <summary>
    /// Computes the storage-safe hash of a raw token received from a client.
    /// </summary>
    string ComputeHash(string rawToken);
}
