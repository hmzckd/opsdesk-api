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

    /// <summary>
    /// Creates a valid Ticket with normalized text and server-owned defaults.
    /// </summary>
    public static Ticket Create(
        Guid requesterId,
        string? title,
        string? description,
        TicketPriority? priority = null)
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

        TicketPriority resolvedPriority =
            priority ?? TicketPriority.Medium;

        if (!Enum.IsDefined(resolvedPriority))
        {
            throw new ArgumentOutOfRangeException(
                nameof(priority),
                priority,
                "Ticket priority is not supported.");
        }

        DateTime nowUtc = DateTime.UtcNow;

        return new Ticket
        {
            Id = Guid.NewGuid(),
            Title = normalizedTitle,
            Description = normalizedDescription,
            Priority = resolvedPriority,
            Status = TicketStatus.Open,
            RequesterId = requesterId,
            AssigneeId = null,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            ResolvedAtUtc = null,
            ClosedAtUtc = null
        };
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
