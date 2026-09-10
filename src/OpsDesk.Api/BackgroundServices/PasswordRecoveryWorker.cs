using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Application.Auth.Models;

namespace OpsDesk.Api.BackgroundServices;

public sealed class PasswordRecoveryWorker(IServiceScopeFactory scopes, IConfiguration configuration,
    TimeProvider timeProvider, ILogger<PasswordRecoveryWorker> logger) : BackgroundService
{
    // Runs independently of HTTP and gives each job a fresh set of scoped services and DbContext.
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("PasswordRecovery:WorkerEnabled", true)) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            do
            {
                try
                {
                    for (int processed = 0; processed < 10; processed++)
                    {
                        if (!await ProcessNextAsync(stoppingToken)) break;
                    }
                }
                catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
                {
                    // Exception messages can contain SMTP details; log only type, never email/token payloads.
                    logger.LogError("Password recovery queue failed ({ErrorType}); retrying on the next poll.",
                        exception.GetType().Name);
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogDebug("Password recovery worker stopped; leased work remains recoverable.");
            return;
        }
    }

    // Leases one job, bounds its delivery time, and records success or a retry without exposing it over HTTP.
    private async Task<bool> ProcessNextAsync(CancellationToken stoppingToken)
    {
        await using AsyncServiceScope scope = scopes.CreateAsyncScope();
        var queue = scope.ServiceProvider.GetRequiredService<IPasswordRecoveryQueue>();
        DateTime nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        PasswordRecoveryWorkItem? job = await queue.ClaimAsync(nowUtc, stoppingToken);
        if (job is null) return false;
        if (job.Attempts > 3 || job.RequestedAtUtc.AddMinutes(30) <= nowUtc)
        {
            await queue.CompleteAsync(job, stoppingToken);
            return true;
        }
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(30));
            var service = scope.ServiceProvider.GetRequiredService<IPasswordRecoveryService>();
            await service.SendAsync(job.Email, job.RequestedAtUtc, deadline.Token);
            await queue.CompleteAsync(job, stoppingToken);
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            logger.LogWarning("Password recovery job {JobId} attempt {Attempt} failed ({ErrorType}).",
                job.Id, job.Attempts, exception.GetType().Name);
            await queue.RetryAsync(job, timeProvider.GetUtcNow().UtcDateTime, stoppingToken);
        }
        return true;
    }
}
