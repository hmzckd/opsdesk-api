using OpsDesk.Application.Tickets.DTOs;
using OpsDesk.Domain.Enums;

namespace OpsDesk.Application.Tickets.Interfaces;

public interface ITicketService
{
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
