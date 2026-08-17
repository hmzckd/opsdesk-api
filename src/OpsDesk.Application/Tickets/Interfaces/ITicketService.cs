using OpsDesk.Application.Tickets.DTOs;

namespace OpsDesk.Application.Tickets.Interfaces;

public interface ITicketService
{
    /// <summary>
    /// Creates a Ticket for the authenticated requester.
    /// </summary>
    Task<TicketResponse> CreateAsync(
        Guid requesterId,
        CreateTicketRequest request,
        CancellationToken cancellationToken = default);
}
