using OpsDesk.Domain.Enums;

namespace OpsDesk.Application.Tickets.DTOs;

/// <summary>
/// Contains the client-controlled fields accepted when creating a Ticket.
/// </summary>
public sealed record CreateTicketRequest(
    string? Title,
    string? Description,
    TicketPriority? Priority = null);
