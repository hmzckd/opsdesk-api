namespace OpsDesk.Api.Configuration;

public sealed class SlaBreachSettings
{
    public const string SectionName = "SlaBreach";
    public const int MaximumPollIntervalSeconds = 24 * 60 * 60;

    public bool WorkerEnabled { get; init; } = true;

    public int PollIntervalSeconds { get; init; } = 60;

    public int BatchSize { get; init; } = 100;
}
