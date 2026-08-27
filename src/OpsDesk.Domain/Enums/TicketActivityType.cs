namespace OpsDesk.Domain.Enums;

/// <summary>
/// Identifies the source and meaning of one Ticket timeline item.
/// </summary>
public enum TicketActivityType
{
    CommentAdded,
    StatusChanged,
    AssignmentChanged
}
