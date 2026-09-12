namespace OpsDesk.Domain.Entities;

/// <summary>
/// Represents the durable fact that OpsDesk detected one Ticket SLA breach.
/// </summary>
public sealed class TicketSlaBreach
{
    private TicketSlaBreach()
    {
    }

    public Guid Id { get; private set; }

    public Guid TicketId { get; private set; }

    public Guid SlaPolicyId { get; private set; }

    public DateTime SlaDeadlineUtc { get; private set; }

    public DateTime DetectedAtUtc { get; private set; }
}
