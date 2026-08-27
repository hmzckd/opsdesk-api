using OpsDesk.Application.Tickets.DTOs;
using OpsDesk.Domain.Enums;

namespace OpsDesk.Application.Tickets.Interfaces;

public interface ITicketService
{
    /// <summary>
    /// Retrieves the role-appropriate combined timeline for a visible Ticket.
    /// </summary>
    Task<IReadOnlyList<TicketActivityResponse>?> GetActivityAsync(
        Guid ticketId,
        Guid viewerId,
        UserRole viewerRole,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Assigns a visible Ticket to an Agent.
    /// </summary>
    Task<TicketResponse?> AssignAsync(
        Guid ticketId,
        Guid assigneeId,
        Guid actorId,
        UserRole actorRole,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the assignee from a visible Ticket.
    /// </summary>
    Task<TicketResponse?> UnassignAsync(
        Guid ticketId,
        Guid actorId,
        UserRole actorRole,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves public Comments for a visible Ticket.
    /// </summary>
    Task<IReadOnlyList<TicketCommentResponse>?> GetCommentsAsync(
        Guid ticketId,
        Guid viewerId,
        UserRole viewerRole,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a public Comment to a visible Ticket.
    /// </summary>
    Task<TicketCommentResponse?> AddCommentAsync(
        Guid ticketId,
        Guid authorId,
        UserRole authorRole,
        AddTicketCommentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves visible status history for an authenticated User.
    /// </summary>
    Task<IReadOnlyList<TicketStatusChangeResponse>?>
        GetStatusHistoryAsync(
            Guid ticketId,
            Guid viewerId,
            UserRole viewerRole,
            CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes one Ticket's status for an authenticated actor.
    /// </summary>
    Task<TicketResponse?> ChangeStatusAsync(
        Guid ticketId,
        Guid actorId,
        UserRole actorRole,
        ChangeTicketStatusRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves one Ticket for an authenticated User.
    /// </summary>
    Task<TicketResponse?> GetByIdAsync(
        Guid ticketId,
        Guid viewerId,
        UserRole viewerRole,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a Ticket for the authenticated requester.
    /// </summary>
    Task<TicketResponse> CreateAsync(
        Guid requesterId,
        CreateTicketRequest request,
        CancellationToken cancellationToken = default);
}
