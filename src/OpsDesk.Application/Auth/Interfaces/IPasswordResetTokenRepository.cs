using OpsDesk.Domain.Entities;

namespace OpsDesk.Application.Auth.Interfaces;

public interface IPasswordResetTokenRepository
{
    // Reads token metadata without tracking; final validity is rechecked under the account lock.
    Task<PasswordResetToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    // Atomically consumes a valid token, changes the password and advances the session version.
    Task<bool> TryResetAsync(string tokenHash, string newPasswordHash, CancellationToken cancellationToken = default);

    // Serializes issuance per account and enforces the sixty-second sending cooldown.
    Task<bool> TryReplaceAsync(PasswordResetToken token, CancellationToken cancellationToken = default);

    // Invalidates a failed delivery without revoking a newer token issued for the same account.
    Task RevokeAsync(Guid tokenId, DateTime revokedAtUtc, CancellationToken cancellationToken = default);
}
