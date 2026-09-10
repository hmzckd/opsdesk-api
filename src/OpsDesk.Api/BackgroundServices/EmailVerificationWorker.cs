using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Application.Auth.Models;

namespace OpsDesk.Api.BackgroundServices;

public sealed class EmailVerificationWorker(
    IServiceScopeFactory scopes,
    IConfiguration configuration,
    TimeProvider timeProvider,
    ILogger<EmailVerificationWorker> logger) : BackgroundService
{
    // Polls the durable queue independently from anonymous HTTP requests.
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("EmailVerification:WorkerEnabled", true))
        {
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            do
            {
                try
                {
                    for (int processed = 0; processed < 10; processed++)
                    {
                        if (!await ProcessNextAsync(stoppingToken))
                        {
                            break;
                        }
                    }
                }
                catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
                {
                    // Queue and database failures are recoverable; log no email or token data.
                    logger.LogError(
                        "Email verification queue failed ({ErrorType}); retrying on the next poll.",
                        exception.GetType().Name);
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogDebug("Email verification worker stopped; leased work remains recoverable.");
            return;
        }
    }

    // Sends one leased request and records completion or a bounded retry.
    private async Task<bool> ProcessNextAsync(CancellationToken stoppingToken)
    {
        await using AsyncServiceScope scope = scopes.CreateAsyncScope();
        var queue = scope.ServiceProvider.GetRequiredService<IEmailVerificationQueue>();
        DateTime nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        EmailVerificationWorkItem? job = await queue.ClaimAsync(nowUtc, stoppingToken);
        if (job is null)
        {
            return false;
        }

        if (job.Attempts > 3 || job.RequestedAtUtc.AddHours(8) <= nowUtc)
        {
            await queue.CompleteAsync(job, stoppingToken);
            return true;
        }

        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(30));
            var service = scope.ServiceProvider.GetRequiredService<IEmailVerificationService>();
            await service.SendQueuedAsync(job.Email, job.RequestedAtUtc, deadline.Token);
            await queue.CompleteAsync(job, stoppingToken);
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            logger.LogWarning(
                "Email verification job {JobId} attempt {Attempt} failed ({ErrorType}).",
                job.Id, job.Attempts, exception.GetType().Name);
            await queue.RetryAsync(
                job,
                timeProvider.GetUtcNow().UtcDateTime,
                stoppingToken);
        }

        return true;
    }
}
