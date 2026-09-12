using OpsDesk.Application.Sla.Interfaces;

namespace OpsDesk.Application.Sla.Services;

public sealed class SlaBreachService : ISlaBreachService
{
    public const int MaximumBatchSize = 100;

    private readonly ISlaBreachRepository _repository;
    private readonly TimeProvider _timeProvider;

    public SlaBreachService(
        ISlaBreachRepository repository,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Uses server-owned UTC time to record one bounded detection pass.
    /// </summary>
    public Task<int> DetectAndRecordAsync(
        int maximumCandidates,
        CancellationToken cancellationToken = default)
    {
        if (maximumCandidates is < 1 or > MaximumBatchSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumCandidates),
                maximumCandidates,
                $"SLA breach batch size must be between 1 and " +
                $"{MaximumBatchSize}.");
        }

        return _repository.RecordNewBreachesAsync(
            _timeProvider.GetUtcNow().UtcDateTime,
            maximumCandidates,
            cancellationToken);
    }
}
