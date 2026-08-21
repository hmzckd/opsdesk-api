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

    /// <summary>
    /// Verifies an open Ticket can enter active work.
    /// </summary>
    [Fact]
    public void ChangeStatus_should_start_work_on_open_ticket()
    {
        Ticket ticket = Ticket.Create(
            Guid.NewGuid(),
            "Printer is unavailable",
            "The printer does not respond.");

        DateTime changedAtUtc = ticket.UpdatedAtUtc.AddMinutes(1);

        ticket.ChangeStatus(
            TicketStatus.InProgress,
            changedAtUtc);

        Assert.Equal(TicketStatus.InProgress, ticket.Status);
        Assert.Equal(changedAtUtc, ticket.UpdatedAtUtc);
        Assert.Null(ticket.ResolvedAtUtc);
        Assert.Null(ticket.ClosedAtUtc);
    }

    /// <summary>
    /// Verifies active work can pause while support waits for the requester.
    /// </summary>
    [Fact]
    public void ChangeStatus_should_wait_for_customer_from_active_work()
    {
        Ticket ticket = Ticket.Create(
            Guid.NewGuid(),
            "Printer is unavailable",
            "The printer does not respond.");

        DateTime workStartedAtUtc =
            ticket.UpdatedAtUtc.AddMinutes(1);
        DateTime waitingStartedAtUtc =
            workStartedAtUtc.AddMinutes(1);

        ticket.ChangeStatus(
            TicketStatus.InProgress,
            workStartedAtUtc);
        ticket.ChangeStatus(
            TicketStatus.WaitingCustomer,
            waitingStartedAtUtc);

        Assert.Equal(TicketStatus.WaitingCustomer, ticket.Status);
        Assert.Equal(waitingStartedAtUtc, ticket.UpdatedAtUtc);
        Assert.Null(ticket.ResolvedAtUtc);
        Assert.Null(ticket.ClosedAtUtc);
    }

    /// <summary>
    /// Verifies work can resume after the requester replies.
    /// </summary>
    [Fact]
    public void ChangeStatus_should_resume_work_after_customer_reply()
    {
        Ticket ticket = Ticket.Create(
            Guid.NewGuid(),
            "Printer is unavailable",
            "The printer does not respond.");

        DateTime changedAtUtc = ticket.UpdatedAtUtc;
        ticket.ChangeStatus(
            TicketStatus.InProgress,
            changedAtUtc = changedAtUtc.AddMinutes(1));
        ticket.ChangeStatus(
            TicketStatus.WaitingCustomer,
            changedAtUtc = changedAtUtc.AddMinutes(1));
        ticket.ChangeStatus(
            TicketStatus.InProgress,
            changedAtUtc = changedAtUtc.AddMinutes(1));

        Assert.Equal(TicketStatus.InProgress, ticket.Status);
        Assert.Equal(changedAtUtc, ticket.UpdatedAtUtc);
        Assert.Null(ticket.ResolvedAtUtc);
        Assert.Null(ticket.ClosedAtUtc);
    }

    /// <summary>
    /// Verifies resolving active work records the resolution time.
    /// </summary>
    [Fact]
    public void ChangeStatus_should_resolve_active_work()
    {
        Ticket ticket = Ticket.Create(
            Guid.NewGuid(),
            "Printer is unavailable",
            "The printer does not respond.");

        DateTime changedAtUtc = ticket.UpdatedAtUtc;
        ticket.ChangeStatus(
            TicketStatus.InProgress,
            changedAtUtc = changedAtUtc.AddMinutes(1));
        ticket.ChangeStatus(
            TicketStatus.Resolved,
            changedAtUtc = changedAtUtc.AddMinutes(1));

        Assert.Equal(TicketStatus.Resolved, ticket.Status);
        Assert.Equal(changedAtUtc, ticket.UpdatedAtUtc);
        Assert.Equal(changedAtUtc, ticket.ResolvedAtUtc);
        Assert.Null(ticket.ClosedAtUtc);
    }

    /// <summary>
    /// Verifies a Ticket can resolve directly after the requester replies.
    /// </summary>
    [Fact]
    public void ChangeStatus_should_resolve_while_waiting_for_customer()
    {
        Ticket ticket = Ticket.Create(
            Guid.NewGuid(),
            "Printer is unavailable",
            "The printer does not respond.");

        DateTime changedAtUtc = ticket.UpdatedAtUtc;
        ticket.ChangeStatus(
            TicketStatus.InProgress,
            changedAtUtc = changedAtUtc.AddMinutes(1));
        ticket.ChangeStatus(
            TicketStatus.WaitingCustomer,
            changedAtUtc = changedAtUtc.AddMinutes(1));
        ticket.ChangeStatus(
            TicketStatus.Resolved,
            changedAtUtc = changedAtUtc.AddMinutes(1));

        Assert.Equal(TicketStatus.Resolved, ticket.Status);
        Assert.Equal(changedAtUtc, ticket.ResolvedAtUtc);
    }

    /// <summary>
    /// Verifies reopening a resolved Ticket clears its resolution time.
    /// </summary>
    [Fact]
    public void ChangeStatus_should_reopen_resolved_ticket()
    {
        Ticket ticket = Ticket.Create(
            Guid.NewGuid(),
            "Printer is unavailable",
            "The printer does not respond.");

        DateTime changedAtUtc = ticket.UpdatedAtUtc;
        ticket.ChangeStatus(
            TicketStatus.InProgress,
            changedAtUtc = changedAtUtc.AddMinutes(1));
        ticket.ChangeStatus(
            TicketStatus.Resolved,
            changedAtUtc = changedAtUtc.AddMinutes(1));
        ticket.ChangeStatus(
            TicketStatus.InProgress,
            changedAtUtc = changedAtUtc.AddMinutes(1));

        Assert.Equal(TicketStatus.InProgress, ticket.Status);
        Assert.Equal(changedAtUtc, ticket.UpdatedAtUtc);
        Assert.Null(ticket.ResolvedAtUtc);
        Assert.Null(ticket.ClosedAtUtc);
    }

    /// <summary>
    /// Verifies closing a resolved Ticket records the closure time.
    /// </summary>
    [Fact]
    public void ChangeStatus_should_close_resolved_ticket()
    {
        Ticket ticket = Ticket.Create(
            Guid.NewGuid(),
            "Printer is unavailable",
            "The printer does not respond.");

        DateTime changedAtUtc = ticket.UpdatedAtUtc;
        ticket.ChangeStatus(
            TicketStatus.InProgress,
            changedAtUtc = changedAtUtc.AddMinutes(1));
        ticket.ChangeStatus(
            TicketStatus.Resolved,
            changedAtUtc = changedAtUtc.AddMinutes(1));
        DateTime resolvedAtUtc = changedAtUtc;
        ticket.ChangeStatus(
            TicketStatus.Closed,
            changedAtUtc = changedAtUtc.AddMinutes(1));

        Assert.Equal(TicketStatus.Closed, ticket.Status);
        Assert.Equal(changedAtUtc, ticket.UpdatedAtUtc);
        Assert.Equal(resolvedAtUtc, ticket.ResolvedAtUtc);
        Assert.Equal(changedAtUtc, ticket.ClosedAtUtc);
    }

    /// <summary>
    /// Verifies lifecycle timestamps cannot use a local or unspecified zone.
    /// </summary>
    [Fact]
    public void ChangeStatus_should_reject_non_utc_time()
    {
        Ticket ticket = Ticket.Create(
            Guid.NewGuid(),
            "Printer is unavailable",
            "The printer does not respond.");

        DateTime originalUpdatedAtUtc = ticket.UpdatedAtUtc;
        var localTime = new DateTime(
            2026,
            8,
            20,
            12,
            0,
            0,
            DateTimeKind.Local);

        Assert.Throws<ArgumentException>(() =>
            ticket.ChangeStatus(TicketStatus.InProgress, localTime));

        Assert.Equal(TicketStatus.Open, ticket.Status);
        Assert.Equal(originalUpdatedAtUtc, ticket.UpdatedAtUtc);
    }

    /// <summary>
    /// Verifies undefined enum values cannot enter the lifecycle state.
    /// </summary>
    [Fact]
    public void ChangeStatus_should_reject_undefined_status()
    {
        Ticket ticket = Ticket.Create(
            Guid.NewGuid(),
            "Printer is unavailable",
            "The printer does not respond.");

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ticket.ChangeStatus((TicketStatus)999, DateTime.UtcNow));

        Assert.Equal(TicketStatus.Open, ticket.Status);
    }

    /// <summary>
    /// Verifies a closed Ticket cannot re-enter the lifecycle.
    /// </summary>
    [Fact]
    public void ChangeStatus_should_keep_closed_ticket_terminal()
    {
        Ticket ticket = Ticket.Create(
            Guid.NewGuid(),
            "Printer is unavailable",
            "The printer does not respond.");

        DateTime changedAtUtc = ticket.UpdatedAtUtc;
        ticket.ChangeStatus(
            TicketStatus.InProgress,
            changedAtUtc = changedAtUtc.AddMinutes(1));
        ticket.ChangeStatus(
            TicketStatus.Resolved,
            changedAtUtc = changedAtUtc.AddMinutes(1));
        ticket.ChangeStatus(
            TicketStatus.Closed,
            changedAtUtc = changedAtUtc.AddMinutes(1));

        Assert.Throws<InvalidOperationException>(() =>
            ticket.ChangeStatus(
                TicketStatus.InProgress,
                changedAtUtc.AddMinutes(1)));

        Assert.Equal(TicketStatus.Closed, ticket.Status);
        Assert.Equal(changedAtUtc, ticket.ClosedAtUtc);
    }
}
