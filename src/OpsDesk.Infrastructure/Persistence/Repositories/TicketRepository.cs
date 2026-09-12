using Microsoft.EntityFrameworkCore;
using OpsDesk.Application.Common.Exceptions;
using OpsDesk.Application.Common.Pagination;
using OpsDesk.Application.Tickets.DTOs;
using OpsDesk.Application.Tickets.Interfaces;
using OpsDesk.Application.Tickets.Queries;
using OpsDesk.Domain.Entities;
using OpsDesk.Domain.Enums;

namespace OpsDesk.Infrastructure.Persistence.Repositories;

public sealed class TicketRepository : ITicketRepository
{
    private readonly OpsDeskDbContext _dbContext;

    public TicketRepository(OpsDeskDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Keeps one multi-step write workflow inside one commit or rollback boundary.
    /// </summary>
    public async Task<TResult> ExecuteInWriteTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        TResult result = await operation(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return result;
    }

    /// <summary>
    /// Projects one bounded, read-only Ticket page inside the viewer's scope.
    /// </summary>
    public async Task<PagedResponse<TicketListItemResponse>>
        GetVisiblePageAsync(
            Guid viewerId,
            UserRole viewerRole,
            TicketListQuery listQuery,
            DateTime observedAtUtc,
            CancellationToken cancellationToken = default)
    {
        IQueryable<Ticket> query =
            _dbContext.Tickets.AsNoTracking();

        query = viewerRole switch
        {
            UserRole.Customer => query.Where(
                ticket => ticket.RequesterId == viewerId),
            UserRole.Agent => query.Where(
                ticket =>
                    ticket.AssigneeId == null ||
                    ticket.AssigneeId == viewerId),
            UserRole.Admin => query,
            _ => query.Where(_ => false)
        };

        if (listQuery.Status is TicketStatus status)
        {
            query = query.Where(ticket => ticket.Status == status);
        }

        if (listQuery.Priority is TicketPriority priority)
        {
            query = query.Where(
                ticket => ticket.Priority == priority);
        }

        if (listQuery.RequesterId is Guid requesterId)
        {
            query = query.Where(
                ticket => ticket.RequesterId == requesterId);
        }

        if (listQuery.AssigneeId is Guid assigneeId)
        {
            query = query.Where(
                ticket => ticket.AssigneeId == assigneeId);
        }

        if (listQuery.Unassigned)
        {
            query = query.Where(ticket => ticket.AssigneeId == null);
        }

        int totalCount = await query.CountAsync(cancellationToken);

        long offset =
            ((long)listQuery.Page - 1) * listQuery.PageSize;
        List<TicketListItemResponse> items;

        if (offset >= totalCount)
        {
            items = [];
        }
        else
        {
            IOrderedQueryable<Ticket> orderedQuery =
                ApplyOrdering(
                    query,
                    listQuery.SortBy,
                    listQuery.SortDirection);

            items = await orderedQuery
                .Skip((int)offset)
                .Take(listQuery.PageSize)
                .Select(ticket => new TicketListItemResponse(
                    ticket.Id,
                    ticket.Title,
                    ticket.Priority,
                    ticket.Status,
                    ticket.RequesterId,
                    ticket.AssigneeId,
                    ticket.CreatedAtUtc,
                    ticket.UpdatedAtUtc,
                    ticket.SlaDeadlineUtc,
                    ticket.ResolvedAtUtc.HasValue
                        ? ticket.ResolvedAtUtc.Value >
                            ticket.SlaDeadlineUtc
                        : observedAtUtc > ticket.SlaDeadlineUtc))
                .ToListAsync(cancellationToken);
        }

        int totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(
                totalCount / (double)listQuery.PageSize);

        return new PagedResponse<TicketListItemResponse>(
            items,
            listQuery.Page,
            listQuery.PageSize,
            totalCount,
            totalPages,
            HasPreviousPage:
                totalPages > 0 && listQuery.Page > 1,
            HasNextPage: listQuery.Page < totalPages);
    }

    /// <summary>
    /// Applies approved ordering with a deterministic Ticket ID tie-breaker.
    /// </summary>
    private static IOrderedQueryable<Ticket> ApplyOrdering(
        IQueryable<Ticket> query,
        TicketSortField sortBy,
        TicketSortDirection sortDirection)
    {
        return (sortBy, sortDirection) switch
        {
            (TicketSortField.CreatedAtUtc,
                TicketSortDirection.Asc) => query
                    .OrderBy(ticket => ticket.CreatedAtUtc)
                    .ThenBy(ticket => ticket.Id),
            (TicketSortField.CreatedAtUtc,
                TicketSortDirection.Desc) => query
                    .OrderByDescending(ticket => ticket.CreatedAtUtc)
                    .ThenByDescending(ticket => ticket.Id),
            (TicketSortField.UpdatedAtUtc,
                TicketSortDirection.Asc) => query
                    .OrderBy(ticket => ticket.UpdatedAtUtc)
                    .ThenBy(ticket => ticket.Id),
            (TicketSortField.UpdatedAtUtc,
                TicketSortDirection.Desc) => query
                    .OrderByDescending(ticket => ticket.UpdatedAtUtc)
                    .ThenByDescending(ticket => ticket.Id),
            (TicketSortField.Priority,
                TicketSortDirection.Asc) => query
                    .OrderBy(ticket =>
                        ticket.Priority == TicketPriority.Low ? 0 :
                        ticket.Priority == TicketPriority.Medium ? 1 :
                        ticket.Priority == TicketPriority.High ? 2 : 3)
                    .ThenBy(ticket => ticket.Id),
            (TicketSortField.Priority,
                TicketSortDirection.Desc) => query
                    .OrderByDescending(ticket =>
                        ticket.Priority == TicketPriority.Low ? 0 :
                        ticket.Priority == TicketPriority.Medium ? 1 :
                        ticket.Priority == TicketPriority.High ? 2 : 3)
                    .ThenByDescending(ticket => ticket.Id),
            _ => throw new ArgumentOutOfRangeException(
                nameof(sortBy),
                sortBy,
                "Ticket sorting is not supported.")
        };
    }

    /// <summary>
    /// Reads existing activity sources, enriches their Users, and orders them.
    /// </summary>
    public async Task<IReadOnlyList<TicketActivityResponse>>
        GetActivityAsync(
            Guid ticketId,
            bool includeAssignmentChanges,
            CancellationToken cancellationToken = default)
    {
        List<TicketActivityResponse> comments = await (
            from comment in _dbContext.TicketComments.AsNoTracking()
            join actor in _dbContext.Users.AsNoTracking()
                on comment.AuthorId equals actor.Id
            where comment.TicketId == ticketId
            select new TicketActivityResponse(
                comment.Id,
                TicketActivityType.CommentAdded,
                new TicketActivityUserResponse(
                    actor.Id,
                    actor.FirstName,
                    actor.LastName,
                    actor.Role),
                comment.CreatedAtUtc,
                comment.Content,
                null,
                null,
                null,
                null))
            .ToListAsync(cancellationToken);

        List<TicketActivityResponse> statusChanges = await (
            from change in _dbContext.TicketStatusChanges.AsNoTracking()
            join actor in _dbContext.Users.AsNoTracking()
                on change.ActorId equals actor.Id
            where change.TicketId == ticketId
            select new TicketActivityResponse(
                change.Id,
                TicketActivityType.StatusChanged,
                new TicketActivityUserResponse(
                    actor.Id,
                    actor.FirstName,
                    actor.LastName,
                    actor.Role),
                change.CreatedAtUtc,
                null,
                change.PreviousStatus,
                change.NewStatus,
                null,
                null))
            .ToListAsync(cancellationToken);

        IReadOnlyList<TicketActivityResponse> assignmentChanges = [];

        if (includeAssignmentChanges)
        {
            assignmentChanges = await (
                from change in
                    _dbContext.TicketAssignmentChanges.AsNoTracking()
                join actor in _dbContext.Users.AsNoTracking()
                    on change.ActorId equals actor.Id
                join previousAssignee in _dbContext.Users.AsNoTracking()
                    on change.PreviousAssigneeId equals
                        (Guid?)previousAssignee.Id
                    into previousAssignees
                from previousAssignee in
                    previousAssignees.DefaultIfEmpty()
                join newAssignee in _dbContext.Users.AsNoTracking()
                    on change.NewAssigneeId equals (Guid?)newAssignee.Id
                    into newAssignees
                from newAssignee in newAssignees.DefaultIfEmpty()
                where change.TicketId == ticketId
                select new TicketActivityResponse(
                    change.Id,
                    TicketActivityType.AssignmentChanged,
                    new TicketActivityUserResponse(
                        actor.Id,
                        actor.FirstName,
                        actor.LastName,
                        actor.Role),
                    change.CreatedAtUtc,
                    null,
                    null,
                    null,
                    previousAssignee == null
                        ? null
                        : new TicketActivityUserResponse(
                            previousAssignee.Id,
                            previousAssignee.FirstName,
                            previousAssignee.LastName,
                            previousAssignee.Role),
                    newAssignee == null
                        ? null
                        : new TicketActivityUserResponse(
                            newAssignee.Id,
                            newAssignee.FirstName,
                            newAssignee.LastName,
                            newAssignee.Role)))
                .ToListAsync(cancellationToken);
        }

        List<TicketActivityResponse> slaBreaches = await _dbContext
            .TicketSlaBreaches
            .AsNoTracking()
            .Where(breach => breach.TicketId == ticketId)
            .Select(breach => new TicketActivityResponse(
                breach.Id,
                TicketActivityType.SlaBreached,
                null,
                breach.DetectedAtUtc,
                null,
                null,
                null,
                null,
                null))
            .ToListAsync(cancellationToken);

        return comments
            .Concat(statusChanges)
            .Concat(assignmentChanges)
            .Concat(slaBreaches)
            .OrderBy(item => item.CreatedAtUtc)
            .ThenBy(item => item.Type)
            .ThenBy(item => item.Id)
            .ToArray();
    }

    /// <summary>
    /// Retrieves Comments ordered by time and stable Comment identity.
    /// </summary>
    public async Task<IReadOnlyList<TicketComment>> GetCommentsAsync(
        Guid ticketId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.TicketComments
            .AsNoTracking()
            .Where(comment => comment.TicketId == ticketId)
            .OrderBy(comment => comment.CreatedAtUtc)
            .ThenBy(comment => comment.Id)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Adds one Ticket Comment to the EF Core change tracker.
    /// </summary>
    public async Task AddCommentAsync(
        TicketComment comment,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.TicketComments.AddAsync(
            comment,
            cancellationToken);
    }

    /// <summary>
    /// Retrieves status history ordered by time and stable entry identity.
    /// </summary>
    public async Task<IReadOnlyList<TicketStatusChange>>
        GetStatusHistoryAsync(
            Guid ticketId,
            CancellationToken cancellationToken = default)
    {
        return await _dbContext.TicketStatusChanges
            .AsNoTracking()
            .Where(change => change.TicketId == ticketId)
            .OrderBy(change => change.CreatedAtUtc)
            .ThenBy(change => change.Id)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Retrieves one Ticket as read-only data.
    /// </summary>
    public Task<Ticket?> GetByIdAsync(
        Guid ticketId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Tickets
            .AsNoTracking()
            .SingleOrDefaultAsync(
                ticket => ticket.Id == ticketId,
                cancellationToken);
    }

    /// <summary>
    /// Retrieves one Ticket only inside the requester's visibility scope.
    /// </summary>
    public Task<Ticket?> GetByIdForRequesterAsync(
        Guid ticketId,
        Guid requesterId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Tickets
            .AsNoTracking()
            .SingleOrDefaultAsync(
                ticket =>
                    ticket.Id == ticketId &&
                    ticket.RequesterId == requesterId,
                cancellationToken);
    }

    /// <summary>
    /// Retrieves an unassigned or Agent-owned Ticket as read-only data.
    /// </summary>
    public Task<Ticket?> GetByIdForAgentAsync(
        Guid ticketId,
        Guid agentId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Tickets
            .AsNoTracking()
            .SingleOrDefaultAsync(
                ticket =>
                    ticket.Id == ticketId &&
                    (ticket.AssigneeId == null ||
                        ticket.AssigneeId == agentId),
                cancellationToken);
    }

    /// <summary>
    /// Retrieves one Ticket with change tracking for a write operation.
    /// </summary>
    public async Task<Ticket?> GetForUpdateAsync(
        Guid ticketId,
        CancellationToken cancellationToken = default)
    {
        if (_dbContext.Database.CurrentTransaction is null)
        {
            return await _dbContext.Tickets.SingleOrDefaultAsync(
                ticket => ticket.Id == ticketId,
                cancellationToken);
        }

        List<Ticket> tickets = await _dbContext.Tickets
            .FromSqlInterpolated(
                $"SELECT * FROM tickets WHERE id = {ticketId} FOR UPDATE")
            .ToListAsync(cancellationToken);

        return tickets.SingleOrDefault();
    }

    /// <summary>
    /// Retrieves one requester-owned Ticket with write tracking enabled.
    /// </summary>
    public async Task<Ticket?> GetForUpdateForRequesterAsync(
        Guid ticketId,
        Guid requesterId,
        CancellationToken cancellationToken = default)
    {
        if (_dbContext.Database.CurrentTransaction is null)
        {
            return await _dbContext.Tickets.SingleOrDefaultAsync(
                ticket =>
                    ticket.Id == ticketId &&
                    ticket.RequesterId == requesterId,
                cancellationToken);
        }

        List<Ticket> tickets = await _dbContext.Tickets
            .FromSqlInterpolated(
                $"SELECT * FROM tickets WHERE id = {ticketId} AND requester_id = {requesterId} FOR UPDATE")
            .ToListAsync(cancellationToken);

        return tickets.SingleOrDefault();
    }

    /// <summary>
    /// Retrieves only an Agent-owned Ticket for a tracked write operation.
    /// </summary>
    public async Task<Ticket?> GetForUpdateAssignedToAgentAsync(
        Guid ticketId,
        Guid agentId,
        CancellationToken cancellationToken = default)
    {
        if (_dbContext.Database.CurrentTransaction is null)
        {
            return await _dbContext.Tickets.SingleOrDefaultAsync(
                ticket =>
                    ticket.Id == ticketId &&
                    ticket.AssigneeId == agentId,
                cancellationToken);
        }

        List<Ticket> tickets = await _dbContext.Tickets
            .FromSqlInterpolated(
                $"SELECT * FROM tickets WHERE id = {ticketId} AND assignee_id = {agentId} FOR UPDATE")
            .ToListAsync(cancellationToken);

        return tickets.SingleOrDefault();
    }

    /// <summary>
    /// Retrieves an unassigned or Agent-owned Ticket for assignment changes.
    /// </summary>
    public Task<Ticket?> GetForAssignmentByAgentAsync(
        Guid ticketId,
        Guid agentId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Tickets.SingleOrDefaultAsync(
            ticket =>
                ticket.Id == ticketId &&
                (ticket.AssigneeId == null ||
                    ticket.AssigneeId == agentId),
            cancellationToken);
    }

    /// <summary>
    /// Adds one status-change record to the EF Core change tracker.
    /// </summary>
    public async Task AddStatusChangeAsync(
        TicketStatusChange statusChange,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.TicketStatusChanges.AddAsync(
            statusChange,
            cancellationToken);
    }

    /// <summary>
    /// Adds one assignment activity to the EF Core change tracker.
    /// </summary>
    public async Task AddAssignmentChangeAsync(
        TicketAssignmentChange assignmentChange,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.TicketAssignmentChanges.AddAsync(
            assignmentChange,
            cancellationToken);
    }

    /// <summary>
    /// Saves all tracked changes in the current DbContext scope.
    /// </summary>
    public async Task SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
            when (exception.Entries.Any(
                entry => entry.Entity is Ticket))
        {
            throw new ConflictException(
                "Ticket was changed by another request. " +
                "Reload it and try again.",
                exception);
        }
    }

    /// <summary>
    /// Adds a Ticket to the EF Core change tracker and saves it.
    /// </summary>
    public async Task AddAsync(
        Ticket ticket,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.Tickets.AddAsync(
            ticket,
            cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
