using OpsDesk.Application.Common.Pagination;
using OpsDesk.Application.Tickets.DTOs;
using OpsDesk.Application.Tickets.Queries;
using OpsDesk.Domain.Entities;
using OpsDesk.Domain.Enums;

namespace OpsDesk.Application.Tickets.Interfaces;

public interface ITicketRepository
{
    /// <summary>
    /// Retrieves one role-scoped, bounded Ticket page.
    /// </summary>
    Task<PagedResponse<TicketListItemResponse>> GetVisiblePageAsync(
        Guid viewerId,
        UserRole viewerRole,
        TicketListQuery query,
        DateTime observedAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves one Ticket's combined timeline in a stable chronological order.
    /// </summary>
    Task<IReadOnlyList<TicketActivityResponse>> GetActivityAsync(
        Guid ticketId,
        bool includeAssignmentChanges,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves Ticket Comments in a stable chronological order.
    /// </summary>
    Task<IReadOnlyList<TicketComment>> GetCommentsAsync(
        Guid ticketId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds one Ticket Comment to the current transaction.
    /// </summary>
    Task AddCommentAsync(
        TicketComment comment,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves status changes in a stable chronological order.
    /// </summary>
    Task<IReadOnlyList<TicketStatusChange>> GetStatusHistoryAsync(
        Guid ticketId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves one Ticket without tracking it for changes.
    /// </summary>
    Task<Ticket?> GetByIdAsync(
        Guid ticketId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves one Ticket only when it belongs to the requester.
    /// </summary>
    Task<Ticket?> GetByIdForRequesterAsync(
        Guid ticketId,
        Guid requesterId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves one Ticket when it is unassigned or belongs to the Agent.
    /// </summary>
    Task<Ticket?> GetByIdForAgentAsync(
        Guid ticketId,
        Guid agentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves one Ticket with EF Core change tracking enabled.
    /// </summary>
    Task<Ticket?> GetForUpdateAsync(
        Guid ticketId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a requester's own Ticket with change tracking enabled.
    /// </summary>
    Task<Ticket?> GetForUpdateForRequesterAsync(
        Guid ticketId,
        Guid requesterId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves one Agent-owned Ticket with change tracking enabled.
    /// </summary>
    Task<Ticket?> GetForUpdateAssignedToAgentAsync(
        Guid ticketId,
        Guid agentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves an unassigned or Agent-owned Ticket for assignment changes.
    /// </summary>
    Task<Ticket?> GetForAssignmentByAgentAsync(
        Guid ticketId,
        Guid agentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds one Ticket status-change record to the current transaction.
    /// </summary>
    Task AddStatusChangeAsync(
        TicketStatusChange statusChange,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds one Ticket assignment activity to the current transaction.
    /// </summary>
    Task AddAssignmentChangeAsync(
        TicketAssignmentChange assignmentChange,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists tracked Ticket changes.
    /// </summary>
    Task SaveChangesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a newly created Ticket.
    /// </summary>
    Task AddAsync(
        Ticket ticket,
        CancellationToken cancellationToken = default);
}
