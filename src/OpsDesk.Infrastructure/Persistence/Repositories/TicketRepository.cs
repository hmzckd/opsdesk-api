using Microsoft.EntityFrameworkCore;
using OpsDesk.Application.Tickets.Interfaces;
using OpsDesk.Domain.Entities;

namespace OpsDesk.Infrastructure.Persistence.Repositories;

public sealed class TicketRepository : ITicketRepository
{
    private readonly OpsDeskDbContext _dbContext;

    public TicketRepository(OpsDeskDbContext dbContext)
    {
        _dbContext = dbContext;
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
    /// Retrieves one Ticket with change tracking for a write operation.
    /// </summary>
    public Task<Ticket?> GetForUpdateAsync(
        Guid ticketId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Tickets.SingleOrDefaultAsync(
            ticket => ticket.Id == ticketId,
            cancellationToken);
    }

    /// <summary>
    /// Retrieves one requester-owned Ticket with write tracking enabled.
    /// </summary>
    public Task<Ticket?> GetForUpdateForRequesterAsync(
        Guid ticketId,
        Guid requesterId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Tickets.SingleOrDefaultAsync(
            ticket =>
                ticket.Id == ticketId &&
                ticket.RequesterId == requesterId,
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
    /// Saves all tracked changes in the current DbContext scope.
    /// </summary>
    public Task SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
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
