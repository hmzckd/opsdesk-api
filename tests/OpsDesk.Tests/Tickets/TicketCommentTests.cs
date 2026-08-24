using OpsDesk.Domain.Entities;

namespace OpsDesk.Tests.Tickets;

public sealed class TicketCommentTests
{
    /// <summary>
    /// Verifies Domain creation normalizes content and keeps server values.
    /// </summary>
    [Fact]
    public void Create_should_normalize_valid_comment()
    {
        Guid ticketId = Guid.NewGuid();
        Guid authorId = Guid.NewGuid();
        DateTime createdAtUtc = DateTime.UtcNow;

        TicketComment comment = TicketComment.Create(
            ticketId,
            authorId,
            "  Please check the network cable.  ",
            createdAtUtc);

        Assert.NotEqual(Guid.Empty, comment.Id);
        Assert.Equal(ticketId, comment.TicketId);
        Assert.Equal(authorId, comment.AuthorId);
        Assert.Equal(
            "Please check the network cable.",
            comment.Content);
        Assert.Equal(createdAtUtc, comment.CreatedAtUtc);
    }

    /// <summary>
    /// Verifies invalid Comment content cannot enter the Domain.
    /// </summary>
    [Theory]
    [MemberData(nameof(InvalidContent))]
    public void Create_should_reject_invalid_content(string? content)
    {
        Assert.Throws<ArgumentException>(() =>
            TicketComment.Create(
                Guid.NewGuid(),
                Guid.NewGuid(),
                content,
                DateTime.UtcNow));
    }

    /// <summary>
    /// Verifies client-controlled or local timestamps are rejected.
    /// </summary>
    [Fact]
    public void Create_should_require_utc_time()
    {
        Assert.Throws<ArgumentException>(() =>
            TicketComment.Create(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Valid content",
                DateTime.Now));
    }

    /// <summary>
    /// Supplies invalid Domain content values.
    /// </summary>
    public static TheoryData<string?> InvalidContent =>
        new()
        {
            null,
            string.Empty,
            "   ",
            new string('C', TicketComment.MaximumContentLength + 1)
        };
}
