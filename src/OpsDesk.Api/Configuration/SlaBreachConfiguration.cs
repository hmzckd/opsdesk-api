using OpsDesk.Api.BackgroundServices;
using OpsDesk.Application.Sla.Services;

namespace OpsDesk.Api.Configuration;

public static class SlaBreachConfiguration
{
    /// <summary>
    /// Binds validated SLA monitoring settings and registers its hosted worker.
    /// </summary>
    public static IServiceCollection AddSlaBreachMonitoring(
        this IServiceCollection services)
    {
        services.AddOptions<SlaBreachSettings>()
            .BindConfiguration(SlaBreachSettings.SectionName)
            .Validate(
                settings =>
                    settings.PollIntervalSeconds is >= 1 and
                        <= SlaBreachSettings.MaximumPollIntervalSeconds,
                $"SlaBreach:PollIntervalSeconds must be between 1 and " +
                    $"{SlaBreachSettings.MaximumPollIntervalSeconds}.")
            .Validate(
                settings =>
                    settings.BatchSize is >= 1 and
                        <= SlaBreachService.MaximumBatchSize,
                $"SlaBreach:BatchSize must be between 1 and " +
                    $"{SlaBreachService.MaximumBatchSize}.")
            .ValidateOnStart();

        services.AddHostedService<SlaBreachWorker>();

        return services;
    }
}
