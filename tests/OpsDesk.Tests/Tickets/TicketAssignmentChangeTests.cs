using OpsDesk.Domain.Entities;

namespace OpsDesk.Tests.Tickets;

public sealed class TicketAssignmentChangeTests
{
    /// <summary>
    /// Verifies a completed assignment change keeps its audit values.
    /// </summary>
    [Fact]
    public void Create_should_record_assignment_change()
    {
        Guid ticketId = Guid.NewGuid();
        Guid actorId = Guid.NewGuid();
        Guid previousAssigneeId = Guid.NewGuid();
        Guid newAssigneeId = Guid.NewGuid();
        DateTime createdAtUtc = DateTime.UtcNow;

        TicketAssignmentChange change =
            TicketAssignmentChange.Create(
                ticketId,
                actorId,
                previousAssigneeId,
                newAssigneeId,
                createdAtUtc);

        Assert.NotEqual(Guid.Empty, change.Id);
        Assert.Equal(ticketId, change.TicketId);
        Assert.Equal(actorId, change.ActorId);
        Assert.Equal(
            previousAssigneeId,
            change.PreviousAssigneeId);
        Assert.Equal(newAssigneeId, change.NewAssigneeId);
        Assert.Equal(createdAtUtc, change.CreatedAtUtc);
    }

    /// <summary>
    /// Verifies activity cannot claim ownership changed when it did not.
    /// </summary>
    [Fact]
    public void Create_should_reject_unchanged_assignee()
    {
        Guid assigneeId = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() =>
            TicketAssignmentChange.Create(
                Guid.NewGuid(),
                Guid.NewGuid(),
                assigneeId,
                assigneeId,
                DateTime.UtcNow));
    }

    /// <summary>
    /// Verifies assignment activity time must be expressed as UTC.
    /// </summary>
    [Fact]
    public void Create_should_reject_non_utc_time()
    {
        var localTime = new DateTime(
            2026,
            8,
            26,
            12,
            0,
            0,
            DateTimeKind.Local);

        Assert.Throws<ArgumentException>(() =>
            TicketAssignmentChange.Create(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                Guid.NewGuid(),
                localTime));
    }
}
