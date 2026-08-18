using OpsDesk.Domain.Entities;

namespace OpsDesk.Application.Tickets.Interfaces;

public interface ITicketRepository
{
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
    /// Persists a newly created Ticket.
    /// </summary>
    Task AddAsync(
        Ticket ticket,
        CancellationToken cancellationToken = default);
}
