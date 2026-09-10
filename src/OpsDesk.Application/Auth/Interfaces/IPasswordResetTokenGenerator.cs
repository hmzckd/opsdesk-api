using OpsDesk.Application.Auth.Models;

namespace OpsDesk.Application.Auth.Interfaces;

public interface IPasswordResetTokenGenerator
{
    // Returns the raw email credential and the hash used for persistence.
    GeneratedPasswordResetToken GenerateToken();

    // Validates the reset-specific format before computing its lookup hash.
    string ComputeHash(string rawToken);
}
