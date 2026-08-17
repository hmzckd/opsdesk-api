using OpsDesk.Application.Tickets.DTOs;
using OpsDesk.Application.Tickets.Interfaces;
using OpsDesk.Domain.Entities;

namespace OpsDesk.Application.Tickets.Services;

public sealed class TicketService : ITicketService
{
    private readonly ITicketRepository _ticketRepository;

    public TicketService(ITicketRepository ticketRepository)
    {
        _ticketRepository = ticketRepository;
    }

    /// <summary>
    /// Creates, persists, and maps a Ticket to its public response.
    /// </summary>
    public async Task<TicketResponse> CreateAsync(
        Guid requesterId,
        CreateTicketRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Ticket ticket = Ticket.Create(
            requesterId,
            request.Title,
            request.Description,
            request.Priority);

        await _ticketRepository.AddAsync(
            ticket,
            cancellationToken);

        return MapToResponse(ticket);
    }

    /// <summary>
    /// Converts a Domain Ticket into the API-safe response contract.
    /// </summary>
    private static TicketResponse MapToResponse(Ticket ticket)
    {
        return new TicketResponse(
            ticket.Id,
            ticket.Title,
            ticket.Description,
            ticket.Priority,
            ticket.Status,
            ticket.RequesterId,
            ticket.AssigneeId,
            ticket.CreatedAtUtc,
            ticket.UpdatedAtUtc,
            ticket.ResolvedAtUtc,
            ticket.ClosedAtUtc);
    }
}
