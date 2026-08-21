using OpsDesk.Domain.Enums;

namespace OpsDesk.Application.Tickets.DTOs;

/// <summary>
/// Represents one user-visible entry in a Ticket's status history.
/// </summary>
public sealed record TicketStatusChangeResponse(
    Guid Id,
    Guid ActorId,
    TicketStatus PreviousStatus,
    TicketStatus NewStatus,
    DateTime CreatedAtUtc);
