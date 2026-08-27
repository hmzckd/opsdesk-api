using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Application.Common.Exceptions;
using OpsDesk.Application.Tickets.DTOs;
using OpsDesk.Application.Tickets.Interfaces;
using OpsDesk.Domain.Entities;
using OpsDesk.Domain.Enums;

namespace OpsDesk.Application.Tickets.Services;

public sealed class TicketService : ITicketService
{
    private readonly ITicketRepository _ticketRepository;
    private readonly IUserRepository _userRepository;

    public TicketService(
        ITicketRepository ticketRepository,
        IUserRepository userRepository)
    {
        _ticketRepository = ticketRepository;
        _userRepository = userRepository;
    }

    /// <summary>
    /// Authorizes and persists one Ticket assignment change.
    /// </summary>
    public async Task<TicketResponse?> AssignAsync(
        Guid ticketId,
        Guid assigneeId,
        Guid actorId,
        UserRole actorRole,
        CancellationToken cancellationToken = default)
    {
        if (actorRole == UserRole.Customer)
        {
            throw new ForbiddenException(
                "Customers cannot assign tickets.");
        }

        if (actorRole == UserRole.Agent && assigneeId != actorId)
        {
            throw new ForbiddenException(
                "Agents can only assign tickets to themselves.");
        }

        Ticket? ticket = actorRole switch
        {
            UserRole.Agent =>
                await _ticketRepository
                    .GetForAssignmentByAgentAsync(
                        ticketId,
                        actorId,
                        cancellationToken),
            UserRole.Admin =>
                await _ticketRepository.GetForUpdateAsync(
                    ticketId,
                    cancellationToken),
            _ => throw new ForbiddenException(
                "This role cannot assign tickets.")
        };

        if (ticket is null)
        {
            return null;
        }

        User? assignee = await _userRepository.GetByIdAsync(
            assigneeId,
            cancellationToken);

        if (assignee is null || assignee.Role != UserRole.Agent)
        {
            throw new ArgumentException(
                "Assignee must be an existing Agent.",
                nameof(assigneeId));
        }

        Guid? previousAssigneeId = ticket.AssigneeId;
        DateTime changedAtUtc = DateTime.UtcNow;
        bool assignmentChanged;

        try
        {
            assignmentChanged = ticket.Assign(
                assigneeId,
                changedAtUtc);
        }
        catch (InvalidOperationException exception)
        {
            throw new ConflictException(
                exception.Message,
                exception);
        }

        if (!assignmentChanged)
        {
            return MapToResponse(ticket);
        }

        TicketAssignmentChange assignmentChange =
            TicketAssignmentChange.Create(
                ticket.Id,
                actorId,
                previousAssigneeId,
                ticket.AssigneeId,
                changedAtUtc);

        await _ticketRepository.AddAssignmentChangeAsync(
            assignmentChange,
            cancellationToken);

        await _ticketRepository.SaveChangesAsync(cancellationToken);

        return MapToResponse(ticket);
    }

    /// <summary>
    /// Authorizes and persists removal of one Ticket assignee.
    /// </summary>
    public async Task<TicketResponse?> UnassignAsync(
        Guid ticketId,
        Guid actorId,
        UserRole actorRole,
        CancellationToken cancellationToken = default)
    {
        if (actorRole == UserRole.Customer)
        {
            throw new ForbiddenException(
                "Customers cannot unassign tickets.");
        }

        Ticket? ticket = actorRole switch
        {
            UserRole.Agent =>
                await _ticketRepository
                    .GetForAssignmentByAgentAsync(
                        ticketId,
                        actorId,
                        cancellationToken),
            UserRole.Admin =>
                await _ticketRepository.GetForUpdateAsync(
                    ticketId,
                    cancellationToken),
            _ => throw new ForbiddenException(
                "This role cannot unassign tickets.")
        };

        if (ticket is null)
        {
            return null;
        }

        Guid? previousAssigneeId = ticket.AssigneeId;
        DateTime changedAtUtc = DateTime.UtcNow;
        bool assignmentChanged;

        try
        {
            assignmentChanged = ticket.Unassign(changedAtUtc);
        }
        catch (InvalidOperationException exception)
        {
            throw new ConflictException(
                exception.Message,
                exception);
        }

        if (!assignmentChanged)
        {
            return MapToResponse(ticket);
        }

        TicketAssignmentChange assignmentChange =
            TicketAssignmentChange.Create(
                ticket.Id,
                actorId,
                previousAssigneeId,
                ticket.AssigneeId,
                changedAtUtc);

        await _ticketRepository.AddAssignmentChangeAsync(
            assignmentChange,
            cancellationToken);

        await _ticketRepository.SaveChangesAsync(cancellationToken);

        return MapToResponse(ticket);
    }

    /// <summary>
    /// Authorizes a viewer and returns one Ticket's public Comments.
    /// </summary>
    public async Task<IReadOnlyList<TicketCommentResponse>?>
        GetCommentsAsync(
            Guid ticketId,
            Guid viewerId,
            UserRole viewerRole,
            CancellationToken cancellationToken = default)
    {
        Ticket? visibleTicket = await GetVisibleTicketAsync(
            ticketId,
            viewerId,
            viewerRole,
            cancellationToken);

        if (visibleTicket is null)
        {
            return null;
        }

        IReadOnlyList<TicketComment> comments =
            await _ticketRepository.GetCommentsAsync(
                ticketId,
                cancellationToken);

        return comments
            .Select(MapToCommentResponse)
            .ToArray();
    }

    /// <summary>
    /// Authorizes, creates, and persists one public Ticket Comment.
    /// </summary>
    public async Task<TicketCommentResponse?> AddCommentAsync(
        Guid ticketId,
        Guid authorId,
        UserRole authorRole,
        AddTicketCommentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Ticket? ticket;

        switch (authorRole)
        {
            case UserRole.Customer:
                ticket =
                    await _ticketRepository
                        .GetForUpdateForRequesterAsync(
                            ticketId,
                            authorId,
                            cancellationToken);
                break;

            case UserRole.Agent:
                ticket = await _ticketRepository
                    .GetForUpdateAssignedToAgentAsync(
                        ticketId,
                        authorId,
                        cancellationToken);
                break;

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

        TicketComment comment;

        try
        {
            comment = ticket.AddComment(
                authorId,
                request.Content,
                DateTime.UtcNow);
        }
        catch (InvalidOperationException exception)
        {
            throw new ConflictException(
                exception.Message,
                exception);
        }

        await _ticketRepository.AddCommentAsync(
            comment,
            cancellationToken);

        await _ticketRepository.SaveChangesAsync(cancellationToken);

        return MapToCommentResponse(comment);
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
        Ticket? visibleTicket = await GetVisibleTicketAsync(
            ticketId,
            viewerId,
            viewerRole,
            cancellationToken);

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
                ticket = await _ticketRepository
                    .GetForUpdateAssignedToAgentAsync(
                        ticketId,
                        actorId,
                        cancellationToken);
                break;

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

        bool supportUserIsClosingTicket =
            actorRole is UserRole.Agent or UserRole.Admin &&
            request.Status == TicketStatus.Closed;

        if (supportUserIsClosingTicket)
        {
            throw new ForbiddenException(
                "Support users can resolve tickets, but only the " +
                "requester can confirm closure.");
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
        Ticket? ticket = await GetVisibleTicketAsync(
            ticketId,
            viewerId,
            viewerRole,
            cancellationToken);

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
    /// Selects the read-only Ticket query for an authenticated viewer.
    /// </summary>
    private Task<Ticket?> GetVisibleTicketAsync(
        Guid ticketId,
        Guid viewerId,
        UserRole viewerRole,
        CancellationToken cancellationToken)
    {
        return viewerRole switch
        {
            UserRole.Customer =>
                _ticketRepository.GetByIdForRequesterAsync(
                    ticketId,
                    viewerId,
                    cancellationToken),
            UserRole.Agent =>
                _ticketRepository.GetByIdForAgentAsync(
                    ticketId,
                    viewerId,
                    cancellationToken),
            UserRole.Admin =>
                _ticketRepository.GetByIdAsync(
                    ticketId,
                    cancellationToken),
            _ => Task.FromResult<Ticket?>(null)
        };
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

    /// <summary>
    /// Converts a Domain Comment into the public response contract.
    /// </summary>
    private static TicketCommentResponse MapToCommentResponse(
        TicketComment comment)
    {
        return new TicketCommentResponse(
            comment.Id,
            comment.TicketId,
            comment.AuthorId,
            comment.Content,
            comment.CreatedAtUtc);
    }
}
