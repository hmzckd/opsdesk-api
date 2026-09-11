using OpsDesk.Domain.Entities;
using OpsDesk.Domain.Enums;

namespace OpsDesk.Application.Sla.Interfaces;

public interface ISlaPolicyRepository
{
    /// <summary>
    /// Retrieves the single active SLA policy for a Ticket priority.
    /// </summary>
    Task<SlaPolicy?> GetActiveForPriorityAsync(
        TicketPriority priority,
        CancellationToken cancellationToken = default);
}
