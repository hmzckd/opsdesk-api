using OpsDesk.Domain.Entities;
using OpsDesk.Domain.Enums;

namespace OpsDesk.Tests.Tickets;

internal static class TicketTestFactory
{
    /// <summary>
    /// Creates a valid SLA-backed Ticket for tests unrelated to SLA selection.
    /// </summary>
    public static Ticket Create(
        Guid requesterId,
        string? title,
        string? description,
        TicketPriority? priority = null,
        Guid? policyId = null,
        DateTime? createdAtUtc = null)
    {
        TicketPriority resolvedPriority =
            priority ?? TicketPriority.Medium;

        int durationMinutes = resolvedPriority switch
        {
            TicketPriority.Low => 7 * 24 * 60,
            TicketPriority.Medium => 4 * 24 * 60,
            TicketPriority.High => 2 * 24 * 60,
            TicketPriority.Urgent => 24 * 60,
            _ => throw new ArgumentOutOfRangeException(
                nameof(priority),
                priority,
                "Ticket priority is not supported.")
        };

        SlaPolicy policy = SlaPolicy.Create(
            policyId ?? Guid.NewGuid(),
            resolvedPriority,
            durationMinutes);

        return Ticket.Create(
            requesterId,
            title,
            description,
            policy,
            createdAtUtc ?? DateTime.UtcNow,
            resolvedPriority);
    }
}
