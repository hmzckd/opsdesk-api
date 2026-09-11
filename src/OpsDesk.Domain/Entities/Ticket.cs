using OpsDesk.Domain.Enums;

namespace OpsDesk.Domain.Entities;

public sealed class Ticket
{
    public const int MaximumTitleLength = 200;
    public const int MaximumDescriptionLength = 5000;

    private Ticket()
    {
    }

    public Guid Id { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public TicketPriority Priority { get; private set; }

    public TicketStatus Status { get; private set; }

    public Guid RequesterId { get; private set; }

    public Guid? AssigneeId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public DateTime? ResolvedAtUtc { get; private set; }

    public DateTime? ClosedAtUtc { get; private set; }

    public Guid SlaPolicyId { get; private set; }

    public DateTime SlaDeadlineUtc { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    /// <summary>
    /// Reports whether the Ticket missed its immutable resolution deadline.
    /// </summary>
    public bool IsSlaBreachedAt(DateTime observedAtUtc)
    {
        if (observedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "SLA observation time must be UTC.",
                nameof(observedAtUtc));
        }

        DateTime effectiveResolutionTime =
            ResolvedAtUtc ?? observedAtUtc;

        return effectiveResolutionTime > SlaDeadlineUtc;
    }

    /// <summary>
    /// Creates a valid Ticket and snapshots its selected SLA deadline.
    /// </summary>
    public static Ticket Create(
        Guid requesterId,
        string? title,
        string? description,
        SlaPolicy slaPolicy,
        DateTime createdAtUtc,
        TicketPriority? priority = null)
    {
        ArgumentNullException.ThrowIfNull(slaPolicy);

        TicketPriority resolvedPriority =
            priority ?? TicketPriority.Medium;

        if (slaPolicy.Priority != resolvedPriority)
        {
            throw new ArgumentException(
                "SLA policy priority must match the Ticket priority.",
                nameof(slaPolicy));
        }

        Ticket ticket = CreateCore(
            requesterId,
            title,
            description,
            resolvedPriority,
            createdAtUtc);

        ticket.SlaPolicyId = slaPolicy.Id;
        ticket.SlaDeadlineUtc =
            slaPolicy.CalculateDeadline(createdAtUtc);

        return ticket;
    }

    /// <summary>
    /// Creates the shared Ticket fields after caller-specific inputs are resolved.
    /// </summary>
    private static Ticket CreateCore(
        Guid requesterId,
        string? title,
        string? description,
        TicketPriority resolvedPriority,
        DateTime createdAtUtc)
    {
        if (requesterId == Guid.Empty)
        {
            throw new ArgumentException(
                "Requester ID cannot be empty.",
                nameof(requesterId));
        }

        string normalizedTitle = NormalizeRequiredText(
            title,
            nameof(title),
            MaximumTitleLength);

        string normalizedDescription = NormalizeRequiredText(
            description,
            nameof(description),
            MaximumDescriptionLength);

        if (!Enum.IsDefined(resolvedPriority))
        {
            throw new ArgumentOutOfRangeException(
                nameof(resolvedPriority),
                resolvedPriority,
                "Ticket priority is not supported.");
        }

        if (createdAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "Ticket creation time must be UTC.",
                nameof(createdAtUtc));
        }

        return new Ticket
        {
            Id = Guid.NewGuid(),
            Title = normalizedTitle,
            Description = normalizedDescription,
            Priority = resolvedPriority,
            Status = TicketStatus.Open,
            RequesterId = requesterId,
            AssigneeId = null,
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = createdAtUtc,
            ResolvedAtUtc = null,
            ClosedAtUtc = null,
            ConcurrencyToken = Guid.NewGuid()
        };
    }

    /// <summary>
    /// Assigns the Ticket and reports whether ownership actually changed.
    /// </summary>
    public bool Assign(
        Guid assigneeId,
        DateTime changedAtUtc)
    {
        if (assigneeId == Guid.Empty)
        {
            throw new ArgumentException(
                "Assignee ID cannot be empty.",
                nameof(assigneeId));
        }

        if (changedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "Assignment change time must be UTC.",
                nameof(changedAtUtc));
        }

        if (Status == TicketStatus.Closed)
        {
            throw new InvalidOperationException(
                "Closed tickets cannot change assignment.");
        }

        if (AssigneeId == assigneeId)
        {
            return false;
        }

        AssigneeId = assigneeId;
        UpdatedAtUtc = changedAtUtc;
        ConcurrencyToken = Guid.NewGuid();

        return true;
    }

    /// <summary>
    /// Removes the assignee and reports whether ownership actually changed.
    /// </summary>
    public bool Unassign(DateTime changedAtUtc)
    {
        if (changedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "Assignment change time must be UTC.",
                nameof(changedAtUtc));
        }

        if (Status == TicketStatus.Closed)
        {
            throw new InvalidOperationException(
                "Closed tickets cannot change assignment.");
        }

        if (AssigneeId is null)
        {
            return false;
        }

        AssigneeId = null;
        UpdatedAtUtc = changedAtUtc;
        ConcurrencyToken = Guid.NewGuid();

        return true;
    }

    /// <summary>
    /// Reopens a resolved Ticket and creates the requester's public reason.
    /// </summary>
    public TicketComment Reopen(
        Guid requesterId,
        string? reason,
        DateTime reopenedAtUtc)
    {
        if (requesterId == Guid.Empty)
        {
            throw new ArgumentException(
                "Requester ID cannot be empty.",
                nameof(requesterId));
        }

        if (requesterId != RequesterId)
        {
            throw new InvalidOperationException(
                "Only the Ticket requester can reopen the Ticket.");
        }

        if (Status != TicketStatus.Resolved)
        {
            throw new InvalidOperationException(
                "Only a resolved Ticket can be reopened.");
        }

        TicketComment reasonComment = TicketComment.Create(
            Id,
            requesterId,
            reason,
            reopenedAtUtc);

        Status = TicketStatus.InProgress;
        UpdatedAtUtc = reopenedAtUtc;
        ResolvedAtUtc = null;
        ConcurrencyToken = Guid.NewGuid();

        return reasonComment;
    }

    /// <summary>
    /// Moves the Ticket to another valid lifecycle status.
    /// </summary>
    public void ChangeStatus(
        TicketStatus newStatus,
        DateTime changedAtUtc)
    {
        if (!Enum.IsDefined(newStatus))
        {
            throw new ArgumentOutOfRangeException(
                nameof(newStatus),
                newStatus,
                "Ticket status is not supported.");
        }

        if (changedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "Status change time must be UTC.",
                nameof(changedAtUtc));
        }

        bool isAllowedTransition =
            (Status == TicketStatus.Open &&
                newStatus == TicketStatus.InProgress) ||
            (Status == TicketStatus.InProgress &&
                (newStatus == TicketStatus.WaitingCustomer ||
                    newStatus == TicketStatus.Resolved)) ||
            (Status == TicketStatus.WaitingCustomer &&
                (newStatus == TicketStatus.InProgress ||
                    newStatus == TicketStatus.Resolved)) ||
            (Status == TicketStatus.Resolved &&
                newStatus == TicketStatus.Closed);

        if (!isAllowedTransition)
        {
            throw new InvalidOperationException(
                $"Ticket status cannot change from {Status} " +
                $"to {newStatus}.");
        }

        Status = newStatus;
        UpdatedAtUtc = changedAtUtc;
        ConcurrencyToken = Guid.NewGuid();

        if (newStatus == TicketStatus.Resolved)
        {
            ResolvedAtUtc = changedAtUtc;
        }
        else if (newStatus == TicketStatus.InProgress)
        {
            ResolvedAtUtc = null;
        }

        if (newStatus == TicketStatus.Closed)
        {
            ClosedAtUtc = changedAtUtc;
        }
    }

    /// <summary>
    /// Adds a public Comment when the Ticket still accepts activity.
    /// </summary>
    public TicketComment AddComment(
        Guid authorId,
        string? content,
        DateTime createdAtUtc)
    {
        if (Status == TicketStatus.Closed)
        {
            throw new InvalidOperationException(
                "Closed tickets cannot receive comments.");
        }

        TicketComment comment = TicketComment.Create(
            Id,
            authorId,
            content,
            createdAtUtc);

        UpdatedAtUtc = createdAtUtc;
        ConcurrencyToken = Guid.NewGuid();

        return comment;
    }

    /// <summary>
    /// Trims required text and enforces its maximum length.
    /// </summary>
    private static string NormalizeRequiredText(
        string? value,
        string parameterName,
        int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                $"{parameterName} cannot be empty.",
                parameterName);
        }

        string normalizedValue = value.Trim();

        if (normalizedValue.Length > maximumLength)
        {
            throw new ArgumentException(
                $"{parameterName} cannot exceed " +
                $"{maximumLength} characters.",
                parameterName);
        }

        return normalizedValue;
    }
}
