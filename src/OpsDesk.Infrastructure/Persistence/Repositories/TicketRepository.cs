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
