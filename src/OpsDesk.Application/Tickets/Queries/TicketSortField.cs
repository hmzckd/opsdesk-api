namespace OpsDesk.Application.Tickets.Queries;

/// <summary>
/// Identifies an approved field used to order Ticket collections.
/// </summary>
public enum TicketSortField
{
    CreatedAtUtc,
    UpdatedAtUtc,
    Priority
}
