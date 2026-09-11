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

        Ticket ticket = TicketTestFactory.Create(
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
        Assert.NotEqual(Guid.Empty, ticket.ConcurrencyToken);
    }

    /// <summary>
    /// Verifies that undefined enum values cannot enter the Domain model.
    /// </summary>
    [Fact]
    public void Create_should_reject_undefined_priority()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TicketTestFactory.Create(
                Guid.NewGuid(),
                "Printer is unavailable",
                "The printer does not respond.",
                (TicketPriority)999));
    }

    /// <summary>
    /// Verifies assigning an unassigned Ticket changes ownership once.
    /// </summary>
    [Fact]
    public void Assign_should_set_assignee_and_update_ticket_version()
    {
        Ticket ticket = TicketTestFactory.Create(
            Guid.NewGuid(),
            "Printer is unavailable",
            "The printer does not respond.");

        Guid originalConcurrencyToken = ticket.ConcurrencyToken;
        Guid assigneeId = Guid.NewGuid();
        DateTime assignedAtUtc = ticket.UpdatedAtUtc.AddMinutes(1);

        bool changed = ticket.Assign(assigneeId, assignedAtUtc);

        Assert.True(changed);
        Assert.Equal(assigneeId, ticket.AssigneeId);
        Assert.Equal(assignedAtUtc, ticket.UpdatedAtUtc);
        Assert.NotEqual(
            originalConcurrencyToken,
            ticket.ConcurrencyToken);
    }

    /// <summary>
    /// Verifies an empty User identity cannot become the assignee.
    /// </summary>
    [Fact]
    public void Assign_should_reject_empty_assignee_id()
    {
        Ticket ticket = TicketTestFactory.Create(
            Guid.NewGuid(),
            "Printer is unavailable",
            "The printer does not respond.");

        Assert.Throws<ArgumentException>(() =>
            ticket.Assign(Guid.Empty, DateTime.UtcNow));

        Assert.Null(ticket.AssigneeId);
    }

    /// <summary>
    /// Verifies repeating the same assignment is a true no-op.
    /// </summary>
    [Fact]
    public void Assign_should_not_change_ticket_for_same_assignee()
    {
        Ticket ticket = TicketTestFactory.Create(
            Guid.NewGuid(),
            "Printer is unavailable",
            "The printer does not respond.");

        Guid assigneeId = Guid.NewGuid();
        DateTime assignedAtUtc = ticket.UpdatedAtUtc.AddMinutes(1);

        Assert.True(ticket.Assign(assigneeId, assignedAtUtc));

        Guid assignedConcurrencyToken = ticket.ConcurrencyToken;

        bool changed = ticket.Assign(
            assigneeId,
            assignedAtUtc.AddMinutes(1));

        Assert.False(changed);
        Assert.Equal(assigneeId, ticket.AssigneeId);
        Assert.Equal(assignedAtUtc, ticket.UpdatedAtUtc);
        Assert.Equal(
            assignedConcurrencyToken,
            ticket.ConcurrencyToken);
    }

    /// <summary>
    /// Verifies unassignment clears ownership and reports a real change.
    /// </summary>
    [Fact]
    public void Unassign_should_clear_assignee_and_update_ticket_version()
    {
        Ticket ticket = TicketTestFactory.Create(
            Guid.NewGuid(),
            "Printer is unavailable",
            "The printer does not respond.");

        Assert.True(
            ticket.Assign(
                Guid.NewGuid(),
                ticket.UpdatedAtUtc.AddMinutes(1)));

        Guid assignedConcurrencyToken = ticket.ConcurrencyToken;
        DateTime unassignedAtUtc =
            ticket.UpdatedAtUtc.AddMinutes(1);

        bool changed = ticket.Unassign(unassignedAtUtc);

        Assert.True(changed);
        Assert.Null(ticket.AssigneeId);
        Assert.Equal(unassignedAtUtc, ticket.UpdatedAtUtc);
        Assert.NotEqual(
            assignedConcurrencyToken,
            ticket.ConcurrencyToken);
    }

    /// <summary>
    /// Verifies removing an already empty assignment is a true no-op.
    /// </summary>
    [Fact]
    public void Unassign_should_not_change_unassigned_ticket()
    {
        Ticket ticket = TicketTestFactory.Create(
            Guid.NewGuid(),
            "Printer is unavailable",
            "The printer does not respond.");

        DateTime originalUpdatedAtUtc = ticket.UpdatedAtUtc;
        Guid originalConcurrencyToken = ticket.ConcurrencyToken;

        bool changed = ticket.Unassign(
            originalUpdatedAtUtc.AddMinutes(1));

        Assert.False(changed);
        Assert.Null(ticket.AssigneeId);
        Assert.Equal(originalUpdatedAtUtc, ticket.UpdatedAtUtc);
        Assert.Equal(
            originalConcurrencyToken,
            ticket.ConcurrencyToken);
    }

    /// <summary>
    /// Verifies assignment timestamps must use the UTC time kind.
    /// </summary>
    [Fact]
    public void Assignment_changes_should_reject_non_utc_time()
    {
        Ticket ticket = TicketTestFactory.Create(
            Guid.NewGuid(),
            "Printer is unavailable",
            "The printer does not respond.");
        var localTime = new DateTime(
            2026,
            8,
            26,
            12,
            0,
            0,
            DateTimeKind.Local);

        Assert.Throws<ArgumentException>(() =>
            ticket.Assign(Guid.NewGuid(), localTime));
        Assert.Throws<ArgumentException>(() =>
            ticket.Unassign(localTime));

        Assert.Null(ticket.AssigneeId);
    }

    /// <summary>
    /// Verifies a Closed Ticket rejects assignment and unassignment.
    /// </summary>
    [Fact]
    public void Assignment_changes_should_reject_closed_ticket()
    {
        Ticket ticket = TicketTestFactory.Create(
            Guid.NewGuid(),
            "Printer is unavailable",
            "The printer does not respond.");

        Guid assigneeId = Guid.NewGuid();
        DateTime changedAtUtc = ticket.UpdatedAtUtc;

        ticket.Assign(
            assigneeId,
            changedAtUtc = changedAtUtc.AddMinutes(1));
        ticket.ChangeStatus(
            TicketStatus.InProgress,
            changedAtUtc = changedAtUtc.AddMinutes(1));
        ticket.ChangeStatus(
            TicketStatus.Resolved,
            changedAtUtc = changedAtUtc.AddMinutes(1));
        ticket.ChangeStatus(
            TicketStatus.Closed,
            changedAtUtc = changedAtUtc.AddMinutes(1));

        Guid closedConcurrencyToken = ticket.ConcurrencyToken;

        Assert.Throws<InvalidOperationException>(() =>
            ticket.Assign(
                Guid.NewGuid(),
                changedAtUtc.AddMinutes(1)));
        Assert.Throws<InvalidOperationException>(() =>
            ticket.Unassign(changedAtUtc.AddMinutes(1)));

        Assert.Equal(assigneeId, ticket.AssigneeId);
        Assert.Equal(closedConcurrencyToken, ticket.ConcurrencyToken);
    }

    /// <summary>
    /// Verifies an open Ticket can enter active work.
    /// </summary>
    [Fact]
    public void ChangeStatus_should_start_work_on_open_ticket()
    {
        Ticket ticket = TicketTestFactory.Create(
            Guid.NewGuid(),
            "Printer is unavailable",
            "The printer does not respond.");

        Guid originalConcurrencyToken = ticket.ConcurrencyToken;
        DateTime changedAtUtc = ticket.UpdatedAtUtc.AddMinutes(1);

        ticket.ChangeStatus(
            TicketStatus.InProgress,
            changedAtUtc);

        Assert.Equal(TicketStatus.InProgress, ticket.Status);
        Assert.Equal(changedAtUtc, ticket.UpdatedAtUtc);
        Assert.NotEqual(
            originalConcurrencyToken,
            ticket.ConcurrencyToken);
        Assert.Null(ticket.ResolvedAtUtc);
        Assert.Null(ticket.ClosedAtUtc);
    }

    /// <summary>
    /// Verifies active work can pause while support waits for the requester.
    /// </summary>
    [Fact]
    public void ChangeStatus_should_wait_for_customer_from_active_work()
    {
        Ticket ticket = TicketTestFactory.Create(
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
        Ticket ticket = TicketTestFactory.Create(
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
        Ticket ticket = TicketTestFactory.Create(
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
        Ticket ticket = TicketTestFactory.Create(
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
    /// Verifies generic status changes cannot bypass the reopen workflow.
    /// </summary>
    [Fact]
    public void ChangeStatus_should_reject_resolved_to_in_progress()
    {
        Ticket ticket = TicketTestFactory.Create(
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

        Guid resolvedConcurrencyToken = ticket.ConcurrencyToken;

        Assert.Throws<InvalidOperationException>(() =>
            ticket.ChangeStatus(
                TicketStatus.InProgress,
                changedAtUtc.AddMinutes(1)));

        Assert.Equal(TicketStatus.Resolved, ticket.Status);
        Assert.Equal(changedAtUtc, ticket.UpdatedAtUtc);
        Assert.Equal(changedAtUtc, ticket.ResolvedAtUtc);
        Assert.Null(ticket.ClosedAtUtc);
        Assert.Equal(
            resolvedConcurrencyToken,
            ticket.ConcurrencyToken);
    }

    /// <summary>
    /// Verifies Reopen changes state and returns the normalized public reason.
    /// </summary>
    [Fact]
    public void Reopen_should_change_status_and_create_reason_comment()
    {
        Guid requesterId = Guid.NewGuid();
        Ticket ticket = TicketTestFactory.Create(
            requesterId,
            "Printer is unavailable",
            "The printer does not respond.");

        DateTime changedAtUtc = ticket.UpdatedAtUtc;
        ticket.ChangeStatus(
            TicketStatus.InProgress,
            changedAtUtc = changedAtUtc.AddMinutes(1));
        ticket.ChangeStatus(
            TicketStatus.Resolved,
            changedAtUtc = changedAtUtc.AddMinutes(1));

        Guid resolvedConcurrencyToken = ticket.ConcurrencyToken;
        DateTime reopenedAtUtc = changedAtUtc.AddMinutes(1);

        TicketComment reasonComment = ticket.Reopen(
            requesterId,
            "  The proposed solution did not work.  ",
            reopenedAtUtc);

        Assert.Equal(TicketStatus.InProgress, ticket.Status);
        Assert.Equal(reopenedAtUtc, ticket.UpdatedAtUtc);
        Assert.Null(ticket.ResolvedAtUtc);
        Assert.NotEqual(
            resolvedConcurrencyToken,
            ticket.ConcurrencyToken);
        Assert.Equal(ticket.Id, reasonComment.TicketId);
        Assert.Equal(requesterId, reasonComment.AuthorId);
        Assert.Equal(
            "The proposed solution did not work.",
            reasonComment.Content);
        Assert.Equal(reopenedAtUtc, reasonComment.CreatedAtUtc);
    }

    /// <summary>
    /// Verifies a different User cannot invoke the Domain reopen behavior.
    /// </summary>
    [Fact]
    public void Reopen_should_reject_non_requester_without_mutating_ticket()
    {
        Guid requesterId = Guid.NewGuid();
        Ticket ticket = TicketTestFactory.Create(
            requesterId,
            "Printer is unavailable",
            "The printer does not respond.");

        DateTime changedAtUtc = ticket.UpdatedAtUtc;
        ticket.ChangeStatus(
            TicketStatus.InProgress,
            changedAtUtc = changedAtUtc.AddMinutes(1));
        ticket.ChangeStatus(
            TicketStatus.Resolved,
            changedAtUtc = changedAtUtc.AddMinutes(1));

        Guid originalConcurrencyToken = ticket.ConcurrencyToken;

        Assert.Throws<InvalidOperationException>(() =>
            ticket.Reopen(
                Guid.NewGuid(),
                "This User is not the requester.",
                changedAtUtc.AddMinutes(1)));

        Assert.Equal(TicketStatus.Resolved, ticket.Status);
        Assert.Equal(changedAtUtc, ticket.UpdatedAtUtc);
        Assert.Equal(changedAtUtc, ticket.ResolvedAtUtc);
        Assert.Equal(originalConcurrencyToken, ticket.ConcurrencyToken);
    }

    /// <summary>
    /// Verifies an unresolved Ticket cannot enter the reopen workflow.
    /// </summary>
    [Fact]
    public void Reopen_should_reject_non_resolved_ticket()
    {
        Guid requesterId = Guid.NewGuid();
        Ticket ticket = TicketTestFactory.Create(
            requesterId,
            "Printer is unavailable",
            "The printer does not respond.");

        DateTime originalUpdatedAtUtc = ticket.UpdatedAtUtc;
        Guid originalConcurrencyToken = ticket.ConcurrencyToken;

        Assert.Throws<InvalidOperationException>(() =>
            ticket.Reopen(
                requesterId,
                "The Ticket is not resolved.",
                originalUpdatedAtUtc.AddMinutes(1)));

        Assert.Equal(TicketStatus.Open, ticket.Status);
        Assert.Equal(originalUpdatedAtUtc, ticket.UpdatedAtUtc);
        Assert.Equal(originalConcurrencyToken, ticket.ConcurrencyToken);
    }

    /// <summary>
    /// Verifies an invalid reason is rejected before lifecycle mutation.
    /// </summary>
    [Fact]
    public void Reopen_should_reject_invalid_reason_without_mutating_ticket()
    {
        Guid requesterId = Guid.NewGuid();
        Ticket ticket = TicketTestFactory.Create(
            requesterId,
            "Printer is unavailable",
            "The printer does not respond.");

        DateTime changedAtUtc = ticket.UpdatedAtUtc;
        ticket.ChangeStatus(
            TicketStatus.InProgress,
            changedAtUtc = changedAtUtc.AddMinutes(1));
        ticket.ChangeStatus(
            TicketStatus.Resolved,
            changedAtUtc = changedAtUtc.AddMinutes(1));

        Guid originalConcurrencyToken = ticket.ConcurrencyToken;

        Assert.Throws<ArgumentException>(() =>
            ticket.Reopen(
                requesterId,
                "   ",
                changedAtUtc.AddMinutes(1)));

        Assert.Equal(TicketStatus.Resolved, ticket.Status);
        Assert.Equal(changedAtUtc, ticket.UpdatedAtUtc);
        Assert.Equal(changedAtUtc, ticket.ResolvedAtUtc);
        Assert.Equal(originalConcurrencyToken, ticket.ConcurrencyToken);
    }

    /// <summary>
    /// Verifies closing a resolved Ticket records the closure time.
    /// </summary>
    [Fact]
    public void ChangeStatus_should_close_resolved_ticket()
    {
        Ticket ticket = TicketTestFactory.Create(
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
        Ticket ticket = TicketTestFactory.Create(
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
        Ticket ticket = TicketTestFactory.Create(
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
        Ticket ticket = TicketTestFactory.Create(
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

    /// <summary>
    /// Verifies adding a Comment refreshes Ticket concurrency state.
    /// </summary>
    [Fact]
    public void AddComment_should_update_ticket_version()
    {
        Ticket ticket = TicketTestFactory.Create(
            Guid.NewGuid(),
            "Printer is unavailable",
            "The printer does not respond.");

        Guid originalConcurrencyToken = ticket.ConcurrencyToken;
        DateTime commentedAtUtc = ticket.UpdatedAtUtc.AddMinutes(1);

        ticket.AddComment(
            Guid.NewGuid(),
            "We are investigating.",
            commentedAtUtc);

        Assert.Equal(commentedAtUtc, ticket.UpdatedAtUtc);
        Assert.NotEqual(
            originalConcurrencyToken,
            ticket.ConcurrencyToken);
    }
}
