using OpsDesk.Application.Auth.Models;

namespace OpsDesk.Application.Auth.Interfaces;

public interface IEmailVerificationQueue
{
    // Persists the public request before any account lookup or SMTP operation.
    Task EnqueueAsync(
        string email,
        DateTime requestedAtUtc,
        CancellationToken cancellationToken = default);

    // Leases one job so multiple workers cannot deliver it simultaneously.
    Task<EmailVerificationWorkItem?> ClaimAsync(
        DateTime nowUtc,
        CancellationToken cancellationToken = default);

    // Deletes a job only while the caller still owns its lease.
    Task CompleteAsync(
        EmailVerificationWorkItem job,
        CancellationToken cancellationToken = default);

    // Releases a failed delivery for retry or removes it after the attempt limit.
    Task RetryAsync(
        EmailVerificationWorkItem job,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);
}
