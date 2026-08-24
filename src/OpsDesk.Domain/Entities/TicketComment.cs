namespace OpsDesk.Domain.Entities;

public sealed class TicketComment
{
    public const int MaximumContentLength = 4000;

    private TicketComment()
    {
    }

    public Guid Id { get; private set; }

    public Guid TicketId { get; private set; }

    public Guid AuthorId { get; private set; }

    public string Content { get; private set; } = string.Empty;

    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>
    /// Creates one valid public Ticket Comment with server-owned identity.
    /// </summary>
    public static TicketComment Create(
        Guid ticketId,
        Guid authorId,
        string? content,
        DateTime createdAtUtc)
    {
        if (ticketId == Guid.Empty)
        {
            throw new ArgumentException(
                "Ticket ID cannot be empty.",
                nameof(ticketId));
        }

        if (authorId == Guid.Empty)
        {
            throw new ArgumentException(
                "Author ID cannot be empty.",
                nameof(authorId));
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException(
                "Comment content cannot be empty.",
                nameof(content));
        }

        string normalizedContent = content.Trim();

        if (normalizedContent.Length > MaximumContentLength)
        {
            throw new ArgumentException(
                $"Comment content cannot exceed " +
                $"{MaximumContentLength} characters.",
                nameof(content));
        }

        if (createdAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "Comment creation time must be UTC.",
                nameof(createdAtUtc));
        }

        return new TicketComment
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            AuthorId = authorId,
            Content = normalizedContent,
            CreatedAtUtc = createdAtUtc
        };
    }
}
