using Microsoft.Extensions.Options;
using OpsDesk.Api.Configuration;
using OpsDesk.Application.Sla.Interfaces;

namespace OpsDesk.Api.BackgroundServices;

public sealed class SlaBreachWorker(
    IServiceScopeFactory scopes,
    IOptions<SlaBreachSettings> options,
    TimeProvider timeProvider,
    ILogger<SlaBreachWorker> logger) : BackgroundService
{
    /// <summary>
    /// Runs one immediate detection pass and then repeats at the configured interval.
    /// </summary>
    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        SlaBreachSettings settings = options.Value;

        if (!settings.WorkerEnabled)
        {
            return;
        }

        using var timer = new PeriodicTimer(
            TimeSpan.FromSeconds(settings.PollIntervalSeconds),
            timeProvider);

        try
        {
            do
            {
                try
                {
                    await ProcessBatchAsync(
                        settings.BatchSize,
                        stoppingToken);
                }
                catch (Exception exception)
                    when (!stoppingToken.IsCancellationRequested)
                {
                    logger.LogError(
                        "SLA breach detection failed ({ErrorType}); " +
                        "retrying on the next poll.",
                        exception.GetType().Name);

                    continue;
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            logger.LogDebug("SLA breach worker stopped.");
            return;
        }
    }

    /// <summary>
    /// Gives one detection pass a fresh scoped service and DbContext.
    /// </summary>
    private async Task ProcessBatchAsync(
        int batchSize,
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopes.CreateAsyncScope();
        ISlaBreachService service = scope.ServiceProvider
            .GetRequiredService<ISlaBreachService>();

        int recordedCount = await service.DetectAndRecordAsync(
            batchSize,
            cancellationToken);

        if (recordedCount > 0)
        {
            logger.LogInformation(
                "Recorded {BreachCount} new SLA breaches.",
                recordedCount);
        }
    }
}
