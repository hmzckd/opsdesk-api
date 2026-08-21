using OpsDesk.Application.Common.Exceptions;
using OpsDesk.Application.Tickets.DTOs;
using OpsDesk.Application.Tickets.Interfaces;
using OpsDesk.Domain.Entities;
using OpsDesk.Domain.Enums;

namespace OpsDesk.Application.Tickets.Services;

public sealed class TicketService : ITicketService
{
    private readonly ITicketRepository _ticketRepository;

    public TicketService(ITicketRepository ticketRepository)
    {
        _ticketRepository = ticketRepository;
    }

    /// <summary>
    /// Authorizes a viewer and returns one Ticket's status history.
    /// </summary>
    public async Task<IReadOnlyList<TicketStatusChangeResponse>?>
        GetStatusHistoryAsync(
            Guid ticketId,
            Guid viewerId,
            UserRole viewerRole,
            CancellationToken cancellationToken = default)
    {
        Ticket? visibleTicket;

        switch (viewerRole)
        {
            case UserRole.Customer:
                visibleTicket =
                    await _ticketRepository.GetByIdForRequesterAsync(
                        ticketId,
                        viewerId,
                        cancellationToken);
                break;

            case UserRole.Agent:
            case UserRole.Admin:
                visibleTicket = await _ticketRepository.GetByIdAsync(
                    ticketId,
                    cancellationToken);
                break;

            default:
                return null;
        }

        if (visibleTicket is null)
        {
            return null;
        }

        IReadOnlyList<TicketStatusChange> history =
            await _ticketRepository.GetStatusHistoryAsync(
                ticketId,
                cancellationToken);

        return history
            .Select(MapToStatusChangeResponse)
            .ToArray();
    }

    /// <summary>
    /// Authorizes and persists one Ticket status transition.
    /// </summary>
    public async Task<TicketResponse?> ChangeStatusAsync(
        Guid ticketId,
        Guid actorId,
        UserRole actorRole,
        ChangeTicketStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Ticket? ticket;

        switch (actorRole)
        {
            case UserRole.Customer:
                ticket =
                    await _ticketRepository
                        .GetForUpdateForRequesterAsync(
                            ticketId,
                            actorId,
                            cancellationToken);
                break;

            case UserRole.Agent:
            case UserRole.Admin:
                ticket = await _ticketRepository.GetForUpdateAsync(
                    ticketId,
                    cancellationToken);
                break;

            default:
                return null;
        }

        if (ticket is null)
        {
            return null;
        }

        bool customerCanChangeStatus =
            ticket.Status == TicketStatus.Resolved &&
            request.Status is
                TicketStatus.InProgress or TicketStatus.Closed;

        if (actorRole == UserRole.Customer &&
            !customerCanChangeStatus)
        {
            throw new ForbiddenException(
                "Customers can only reopen or close " +
                "their own resolved tickets.");
        }

        TicketStatus previousStatus = ticket.Status;
        DateTime changedAtUtc = DateTime.UtcNow;

        try
        {
            ticket.ChangeStatus(request.Status, changedAtUtc);
        }
        catch (InvalidOperationException exception)
        {
            throw new ConflictException(
                exception.Message,
                exception);
        }

        TicketStatusChange statusChange = TicketStatusChange.Create(
            ticket.Id,
            actorId,
            previousStatus,
            ticket.Status,
            changedAtUtc);

        await _ticketRepository.AddStatusChangeAsync(
            statusChange,
            cancellationToken);

        await _ticketRepository.SaveChangesAsync(cancellationToken);

        return MapToResponse(ticket);
    }

    /// <summary>
    /// Retrieves one Ticket and maps it to the public response contract.
    /// </summary>
    public async Task<TicketResponse?> GetByIdAsync(
        Guid ticketId,
        Guid viewerId,
        UserRole viewerRole,
        CancellationToken cancellationToken = default)
    {
        Ticket? ticket;

        switch (viewerRole)
        {
            case UserRole.Customer:
                ticket =
                    await _ticketRepository.GetByIdForRequesterAsync(
                        ticketId,
                        viewerId,
                        cancellationToken);
                break;

            case UserRole.Agent:
            case UserRole.Admin:
                ticket = await _ticketRepository.GetByIdAsync(
                    ticketId,
                    cancellationToken);
                break;

            default:
                return null;
        }

        return ticket is null ? null : MapToResponse(ticket);
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

    /// <summary>
    /// Converts a Domain status change into the public response contract.
    /// </summary>
    private static TicketStatusChangeResponse MapToStatusChangeResponse(
        TicketStatusChange statusChange)
    {
        return new TicketStatusChangeResponse(
            statusChange.Id,
            statusChange.ActorId,
            statusChange.PreviousStatus,
            statusChange.NewStatus,
            statusChange.CreatedAtUtc);
    }
}
