using Microsoft.EntityFrameworkCore;
using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Application.Auth.Models;
using OpsDesk.Infrastructure.Persistence.Entities;

namespace OpsDesk.Infrastructure.Persistence.Repositories;

public sealed class EmailVerificationQueue(OpsDeskDbContext database) :
    IEmailVerificationQueue
{
    // Claims work in a short transaction and releases locks before SMTP begins.
    public async Task<EmailVerificationWorkItem?> ClaimAsync(
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await database.Database.BeginTransactionAsync(cancellationToken);
        List<EmailVerificationJob> jobs = await database.EmailVerificationJobs
            .FromSqlInterpolated($"SELECT * FROM email_verification_jobs WHERE available_at_utc <= {nowUtc} ORDER BY available_at_utc, id LIMIT 1 FOR UPDATE SKIP LOCKED")
            .ToListAsync(cancellationToken);
        EmailVerificationJob? job = jobs.SingleOrDefault();
        if (job is null)
        {
            return null;
        }

        job.LeaseId = Guid.NewGuid();
        job.AvailableAtUtc = nowUtc.AddMinutes(2);
        job.Attempts++;
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new EmailVerificationWorkItem(
            job.Id, job.Email, job.RequestedAtUtc,
            job.LeaseId.Value, job.Attempts);
    }

    public Task CompleteAsync(
        EmailVerificationWorkItem job,
        CancellationToken cancellationToken = default)
    {
        return database.EmailVerificationJobs
            .Where(row => row.Id == job.Id && row.LeaseId == job.LeaseId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public Task RetryAsync(
        EmailVerificationWorkItem job,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        if (job.Attempts >= 3)
        {
            return CompleteAsync(job, cancellationToken);
        }

        return database.EmailVerificationJobs
            .Where(row => row.Id == job.Id && row.LeaseId == job.LeaseId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(row => row.LeaseId, (Guid?)null)
                .SetProperty(row => row.AvailableAtUtc, nowUtc.AddSeconds(60)),
                cancellationToken);
    }

    public async Task EnqueueAsync(
        string email,
        DateTime requestedAtUtc,
        CancellationToken cancellationToken = default)
    {
        database.EmailVerificationJobs.Add(new EmailVerificationJob
        {
            Email = email,
            RequestedAtUtc = requestedAtUtc,
            AvailableAtUtc = requestedAtUtc
        });
        await database.SaveChangesAsync(cancellationToken);
    }
}
