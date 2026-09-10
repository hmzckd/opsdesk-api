using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Application.Auth.Models;
using Microsoft.EntityFrameworkCore;
using OpsDesk.Infrastructure.Persistence.Entities;

namespace OpsDesk.Infrastructure.Persistence.Repositories;

public sealed class PasswordRecoveryQueue(OpsDeskDbContext database) : IPasswordRecoveryQueue
{
    // Claims a job in a short transaction, releasing database locks before any SMTP operation.
    public async Task<PasswordRecoveryWorkItem?> ClaimAsync(DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        List<PasswordRecoveryJob> jobs = await database.PasswordRecoveryJobs.FromSqlInterpolated(
            $"SELECT * FROM password_recovery_jobs WHERE available_at_utc <= {nowUtc} ORDER BY available_at_utc, id LIMIT 1 FOR UPDATE SKIP LOCKED")
            .ToListAsync(cancellationToken);
        PasswordRecoveryJob? job = jobs.SingleOrDefault();
        if (job is null) return null;
        job.LeaseId = Guid.NewGuid();
        job.AvailableAtUtc = nowUtc.AddMinutes(2);
        job.Attempts++;
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new PasswordRecoveryWorkItem(job.Id, job.Email, job.RequestedAtUtc, job.LeaseId.Value, job.Attempts);
    }

    // A stale worker cannot delete a job that has since been leased by another worker.
    public Task CompleteAsync(PasswordRecoveryWorkItem job, CancellationToken cancellationToken = default)
    {
        return database.PasswordRecoveryJobs.Where(row => row.Id == job.Id && row.LeaseId == job.LeaseId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    // Retries after sixty seconds, with at most three delivery attempts for a queued request.
    public Task RetryAsync(PasswordRecoveryWorkItem job, DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        if (job.Attempts >= 3) return CompleteAsync(job, cancellationToken);
        return database.PasswordRecoveryJobs.Where(row => row.Id == job.Id && row.LeaseId == job.LeaseId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(row => row.LeaseId, (Guid?)null)
                .SetProperty(row => row.AvailableAtUtc, nowUtc.AddSeconds(60)), cancellationToken);
    }

    // Uses the same INSERT path for every syntactically valid email address.
    public async Task EnqueueAsync(string email, DateTime requestedAtUtc,
        CancellationToken cancellationToken = default)
    {
        database.PasswordRecoveryJobs.Add(new PasswordRecoveryJob
        {
            Email = email,
            RequestedAtUtc = requestedAtUtc,
            AvailableAtUtc = requestedAtUtc
        });
        await database.SaveChangesAsync(cancellationToken);
    }
}
