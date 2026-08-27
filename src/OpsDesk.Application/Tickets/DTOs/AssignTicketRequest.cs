namespace OpsDesk.Application.Tickets.DTOs;

/// <summary>
/// Carries the Agent selected as a Ticket's new assignee.
/// </summary>
public sealed record AssignTicketRequest(Guid AssigneeId);
