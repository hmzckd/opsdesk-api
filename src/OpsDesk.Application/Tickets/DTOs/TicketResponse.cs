using OpsDesk.Domain.Enums;

namespace OpsDesk.Application.Tickets.DTOs;

/// <summary>
/// Represents the public Ticket data returned after creation.
/// </summary>
public sealed record TicketResponse(
    Guid Id,
    string Title,
    string Description,
    TicketPriority Priority,
    TicketStatus Status,
    Guid RequesterId,
    Guid? AssigneeId,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? ResolvedAtUtc,
    DateTime? ClosedAtUtc,
    DateTime SlaDeadlineUtc,
    bool IsSlaBreached);
