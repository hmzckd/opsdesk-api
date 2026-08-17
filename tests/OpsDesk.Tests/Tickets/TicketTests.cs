using OpsDesk.Domain.Entities;
using OpsDesk.Domain.Enums;

namespace OpsDesk.Tests.Tickets;

public sealed class TicketTests
{
    /// <summary>
    /// Verifies the defaults and normalization applied by Ticket.Create.
    /// </summary>
    [Fact]
    public void Create_should_apply_server_owned_defaults()
    {
        Guid requesterId = Guid.NewGuid();

        Ticket ticket = Ticket.Create(
            requesterId,
            "  Printer is unavailable  ",
            "  The printer does not respond.  ");

        Assert.NotEqual(Guid.Empty, ticket.Id);
        Assert.Equal(requesterId, ticket.RequesterId);
        Assert.Equal("Printer is unavailable", ticket.Title);
        Assert.Equal(
            "The printer does not respond.",
            ticket.Description);
        Assert.Equal(TicketStatus.Open, ticket.Status);
        Assert.Equal(TicketPriority.Medium, ticket.Priority);
        Assert.Null(ticket.AssigneeId);
        Assert.Equal(ticket.CreatedAtUtc, ticket.UpdatedAtUtc);
        Assert.Equal(DateTimeKind.Utc, ticket.CreatedAtUtc.Kind);
        Assert.Null(ticket.ResolvedAtUtc);
        Assert.Null(ticket.ClosedAtUtc);
    }

    /// <summary>
    /// Verifies that undefined enum values cannot enter the Domain model.
    /// </summary>
    [Fact]
    public void Create_should_reject_undefined_priority()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Ticket.Create(
                Guid.NewGuid(),
                "Printer is unavailable",
                "The printer does not respond.",
                (TicketPriority)999));
    }
}
