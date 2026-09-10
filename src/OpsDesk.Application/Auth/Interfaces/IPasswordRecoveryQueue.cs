using OpsDesk.Application.Auth.Models;

namespace OpsDesk.Application.Auth.Interfaces;

public interface IPasswordRecoveryQueue
{
    // Persists the request independently of account lookup and SMTP delivery.
    Task EnqueueAsync(string email, DateTime requestedAtUtc, CancellationToken cancellationToken = default);

    // Leases one pending job; another worker may recover it if the lease expires.
    Task<PasswordRecoveryWorkItem?> ClaimAsync(DateTime nowUtc, CancellationToken cancellationToken = default);

    // Deletes a completed job only when the caller still owns its lease.
    Task CompleteAsync(PasswordRecoveryWorkItem job, CancellationToken cancellationToken = default);

    // Releases a failed job for a later attempt, or discards it after the retry limit.
    Task RetryAsync(PasswordRecoveryWorkItem job, DateTime nowUtc, CancellationToken cancellationToken = default);
}
