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
