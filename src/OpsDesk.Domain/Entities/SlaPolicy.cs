using OpsDesk.Domain.Enums;

namespace OpsDesk.Domain.Entities;

public sealed class SlaPolicy
{
    private SlaPolicy()
    {
    }

    public Guid Id { get; private set; }

    public TicketPriority Priority { get; private set; }

    public int ResolutionDurationMinutes { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>
    /// Creates an active SLA policy with a positive resolution duration.
    /// </summary>
    public static SlaPolicy Create(
        Guid id,
        TicketPriority priority,
        int resolutionDurationMinutes)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "SLA policy ID cannot be empty.",
                nameof(id));
        }

        if (!Enum.IsDefined(priority))
        {
            throw new ArgumentOutOfRangeException(
                nameof(priority),
                priority,
                "Ticket priority is not supported.");
        }

        if (resolutionDurationMinutes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(resolutionDurationMinutes),
                resolutionDurationMinutes,
                "SLA resolution duration must be positive.");
        }

        return new SlaPolicy
        {
            Id = id,
            Priority = priority,
            ResolutionDurationMinutes = resolutionDurationMinutes,
            IsActive = true
        };
    }

    /// <summary>
    /// Calculates the resolution deadline from a server-owned UTC start time.
    /// </summary>
    public DateTime CalculateDeadline(DateTime createdAtUtc)
    {
        if (createdAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "Ticket creation time must be UTC.",
                nameof(createdAtUtc));
        }

        if (!IsActive)
        {
            throw new InvalidOperationException(
                "An inactive SLA policy cannot be selected for a new Ticket.");
        }

        return createdAtUtc.AddMinutes(ResolutionDurationMinutes);
    }
}
