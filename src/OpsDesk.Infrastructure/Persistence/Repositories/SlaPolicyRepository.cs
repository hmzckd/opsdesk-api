using Microsoft.EntityFrameworkCore;
using OpsDesk.Application.Sla.Interfaces;
using OpsDesk.Domain.Entities;
using OpsDesk.Domain.Enums;

namespace OpsDesk.Infrastructure.Persistence.Repositories;

public sealed class SlaPolicyRepository : ISlaPolicyRepository
{
    private readonly OpsDeskDbContext _dbContext;

    public SlaPolicyRepository(OpsDeskDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Reads the active policy without enabling EF change tracking.
    /// </summary>
    public Task<SlaPolicy?> GetActiveForPriorityAsync(
        TicketPriority priority,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.SlaPolicies
            .AsNoTracking()
            .SingleOrDefaultAsync(
                policy =>
                    policy.Priority == priority &&
                    policy.IsActive,
                cancellationToken);
    }
}
