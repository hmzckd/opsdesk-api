using OpsDesk.Domain.Enums;

namespace OpsDesk.Application.Tickets.Queries;

/// <summary>
/// Contains validated, typed values used to query visible Tickets.
/// </summary>
public sealed record TicketListQuery(
    int Page,
    int PageSize,
    TicketStatus? Status,
    TicketPriority? Priority,
    Guid? RequesterId,
    Guid? AssigneeId,
    bool Unassigned,
    TicketSortField SortBy,
    TicketSortDirection SortDirection);
