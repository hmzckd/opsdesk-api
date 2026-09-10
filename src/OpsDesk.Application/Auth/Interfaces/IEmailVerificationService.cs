using OpsDesk.Domain.Entities;

namespace OpsDesk.Application.Auth.Interfaces;

public interface IEmailVerificationService
{
    /// <summary>
    /// Issues and sends a verification token for an unverified User.
    /// </summary>
    Task IssueAsync(
        User user,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resends verification without revealing whether an email exists.
    /// </summary>
    Task ResendAsync(
        string email,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes one durable resend request outside the HTTP response.
    /// </summary>
    Task SendQueuedAsync(
        string email,
        DateTime requestedAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies a User with a valid token and consumes that token.
    /// </summary>
    Task ConfirmAsync(
        string rawToken,
        CancellationToken cancellationToken = default);
}
