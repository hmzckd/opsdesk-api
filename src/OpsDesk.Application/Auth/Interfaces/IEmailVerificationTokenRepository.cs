using OpsDesk.Domain.Entities;
using OpsDesk.Application.Auth.Models;

namespace OpsDesk.Application.Auth.Interfaces;

public interface IEmailVerificationTokenRepository
{
    /// <summary>
    /// Retrieves the active token for one User as read-only data.
    /// </summary>
    Task<EmailVerificationToken?> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a token by hash as read-only data.
    /// </summary>
    Task<EmailVerificationToken?> GetByHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Serializes token issuance per User and enforces the resend cooldown atomically.
    /// </summary>
    Task<EmailVerificationTokenReplacement> TryReplaceAsync(
        EmailVerificationToken token,
        TimeSpan cooldown,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes an undelivered replacement and restores the previously active token.
    /// </summary>
    Task RestoreAsync(
        Guid replacementTokenId,
        EmailVerificationToken? previousToken,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies the matching User and consumes the token in one transaction.
    /// </summary>
    Task<bool> TryConfirmAsync(
        string tokenHash,
        User user,
        DateTime verifiedAtUtc,
        CancellationToken cancellationToken = default);
}
