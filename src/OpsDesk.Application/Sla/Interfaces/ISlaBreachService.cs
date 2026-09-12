namespace OpsDesk.Application.Sla.Interfaces;

public interface ISlaBreachService
{
    /// <summary>
    /// Detects overdue Tickets at server time and returns the number recorded.
    /// </summary>
    Task<int> DetectAndRecordAsync(
        int maximumCandidates,
        CancellationToken cancellationToken = default);
}
