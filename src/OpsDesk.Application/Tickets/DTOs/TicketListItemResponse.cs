using OpsDesk.Domain.Enums;

namespace OpsDesk.Application.Tickets.DTOs;

/// <summary>
/// Represents the compact Ticket data returned in collection responses.
/// </summary>
public sealed record TicketListItemResponse(
    Guid Id,
    string Title,
    TicketPriority Priority,
    TicketStatus Status,
    Guid RequesterId,
    Guid? AssigneeId,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
