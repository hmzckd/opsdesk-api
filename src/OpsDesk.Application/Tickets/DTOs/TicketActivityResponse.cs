using OpsDesk.Domain.Enums;

namespace OpsDesk.Application.Tickets.DTOs;

/// <summary>
/// Represents one comment, status, assignment, or SLA item in a Ticket timeline.
/// </summary>
public sealed record TicketActivityResponse(
    Guid Id,
    TicketActivityType Type,
    TicketActivityUserResponse? Actor,
    DateTime CreatedAtUtc,
    string? CommentContent,
    TicketStatus? PreviousStatus,
    TicketStatus? NewStatus,
    TicketActivityUserResponse? PreviousAssignee,
    TicketActivityUserResponse? NewAssignee);
