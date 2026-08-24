namespace OpsDesk.Application.Tickets.DTOs;

/// <summary>
/// Represents one public Ticket Comment returned by the application.
/// </summary>
public sealed record TicketCommentResponse(
    Guid Id,
    Guid TicketId,
    Guid AuthorId,
    string Content,
    DateTime CreatedAtUtc);
