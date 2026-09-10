using OpsDesk.Application.Auth.DTOs;

namespace OpsDesk.Application.Auth.Interfaces;

public interface IPasswordRecoveryService
{
    // Accepts a recovery request without revealing whether an account exists.
    Task RequestAsync(string email, CancellationToken cancellationToken = default);

    // Resets the password and invalidates previous sessions using a single-use reset credential.
    Task ResetAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default);

    // Processes a queued request; unknown or ineligible accounts produce no email.
    Task SendAsync(string email, DateTime requestedAtUtc, CancellationToken cancellationToken = default);
}
