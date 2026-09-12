namespace OpsDesk.Application.Sla.Interfaces;

public interface ISlaBreachRepository
{
    /// <summary>
    /// Atomically finds and records a bounded set of newly breached Tickets.
    /// </summary>
    Task<int> RecordNewBreachesAsync(
        DateTime detectedAtUtc,
        int maximumCandidates,
        CancellationToken cancellationToken = default);
}
