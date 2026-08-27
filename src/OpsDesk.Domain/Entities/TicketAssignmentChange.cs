namespace OpsDesk.Domain.Entities;

public sealed class TicketAssignmentChange
{
    private TicketAssignmentChange()
    {
    }

    public Guid Id { get; private set; }

    public Guid TicketId { get; private set; }

    public Guid ActorId { get; private set; }

    public Guid? PreviousAssigneeId { get; private set; }

    public Guid? NewAssigneeId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>
    /// Records one completed Ticket assignment transition.
    /// </summary>
    public static TicketAssignmentChange Create(
        Guid ticketId,
        Guid actorId,
        Guid? previousAssigneeId,
        Guid? newAssigneeId,
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

        if (previousAssigneeId == Guid.Empty)
        {
            throw new ArgumentException(
                "Previous assignee ID cannot be empty.",
                nameof(previousAssigneeId));
        }

        if (newAssigneeId == Guid.Empty)
        {
            throw new ArgumentException(
                "New assignee ID cannot be empty.",
                nameof(newAssigneeId));
        }

        if (previousAssigneeId == newAssigneeId)
        {
            throw new ArgumentException(
                "An assignment change must modify ownership.",
                nameof(newAssigneeId));
        }

        if (createdAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "Assignment change time must be UTC.",
                nameof(createdAtUtc));
        }

        return new TicketAssignmentChange
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            ActorId = actorId,
            PreviousAssigneeId = previousAssigneeId,
            NewAssigneeId = newAssigneeId,
            CreatedAtUtc = createdAtUtc
        };
    }
}
