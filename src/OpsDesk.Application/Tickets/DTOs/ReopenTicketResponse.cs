namespace OpsDesk.Application.Tickets.DTOs;

/// <summary>
/// Returns the reopened Ticket and its persisted public reason.
/// </summary>
public sealed record ReopenTicketResponse(
    TicketResponse Ticket,
    TicketCommentResponse ReasonComment);
