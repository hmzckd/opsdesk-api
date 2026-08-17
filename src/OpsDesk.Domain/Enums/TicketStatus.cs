namespace OpsDesk.Domain.Enums;

/// <summary>
/// Represents the current lifecycle state of a Ticket.
/// </summary>
public enum TicketStatus
{
    Open,
    InProgress,
    WaitingCustomer,
    Resolved,
    Closed
}
