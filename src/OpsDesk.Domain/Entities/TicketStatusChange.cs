using OpsDesk.Domain.Enums;

namespace OpsDesk.Domain.Entities;

public sealed class TicketStatusChange
{
    private TicketStatusChange()
    {
    }

    public Guid Id { get; private set; }

    public Guid TicketId { get; private set; }

    public Guid ActorId { get; private set; }

    public TicketStatus PreviousStatus { get; private set; }

    public TicketStatus NewStatus { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>
    /// Records one completed Ticket lifecycle transition.
    /// </summary>
    public static TicketStatusChange Create(
        Guid ticketId,
        Guid actorId,
        TicketStatus previousStatus,
        TicketStatus newStatus,
        DateTime createdAtUtc)
    {
        if (ticketId == Guid.Empty)
        {
            throw new ArgumentException(
                "Ticket ID cannot be empty.",
                nameof(ticketId));
        }

        if (actorId == Guid.Empty)
        {
            throw new ArgumentException(
                "Actor ID cannot be empty.",
                nameof(actorId));
        }

        if (previousStatus == newStatus)
        {
            throw new ArgumentException(
                "A status change must move to a different status.",
                nameof(newStatus));
        }

        if (createdAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "Status change time must be UTC.",
                nameof(createdAtUtc));
        }

        return new TicketStatusChange
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            ActorId = actorId,
            PreviousStatus = previousStatus,
            NewStatus = newStatus,
            CreatedAtUtc = createdAtUtc
        };
    }
}
