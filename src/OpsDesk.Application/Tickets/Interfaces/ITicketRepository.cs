using OpsDesk.Domain.Entities;

namespace OpsDesk.Application.Tickets.Interfaces;

public interface ITicketRepository
{
    /// <summary>
    /// Persists a newly created Ticket.
    /// </summary>
    Task AddAsync(
        Ticket ticket,
        CancellationToken cancellationToken = default);
}
