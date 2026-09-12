using OpsDesk.Application.Sla.Interfaces;
using OpsDesk.Application.Sla.Services;

namespace OpsDesk.Tests.Sla;

public sealed class SlaBreachServiceTests
{
    /// <summary>
    /// Verifies callers cannot exceed the approved detection batch boundary.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task Invalid_batch_size_should_be_rejected(
        int batchSize)
    {
        var service = new SlaBreachService(
            new UnusedSlaBreachRepository(),
            TimeProvider.System);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.DetectAndRecordAsync(batchSize));
    }

    private sealed class UnusedSlaBreachRepository :
        ISlaBreachRepository
    {
        public Task<int> RecordNewBreachesAsync(
            DateTime detectedAtUtc,
            int maximumCandidates,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(
                "Invalid input must be rejected before persistence.");
        }
    }
}
