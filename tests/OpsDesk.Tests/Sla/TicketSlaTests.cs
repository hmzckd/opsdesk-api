using OpsDesk.Domain.Entities;
using OpsDesk.Domain.Enums;

namespace OpsDesk.Tests.Sla;

public sealed class TicketSlaTests
{
    /// <summary>
    /// Verifies every approved priority duration with one shared test body.
    /// </summary>
    [Theory]
    [InlineData(TicketPriority.Low, 7 * 24 * 60)]
    [InlineData(TicketPriority.Medium, 4 * 24 * 60)]
    [InlineData(TicketPriority.High, 2 * 24 * 60)]
    [InlineData(TicketPriority.Urgent, 24 * 60)]
    public void Create_should_apply_priority_sla_duration(
        TicketPriority priority,
        int expectedDurationMinutes)
    {
        DateTime createdAtUtc = new(
            2026,
            9,
            11,
            12,
            0,
            0,
            DateTimeKind.Utc);

        SlaPolicy policy = SlaPolicy.Create(
            Guid.NewGuid(),
            priority,
            expectedDurationMinutes);

        Ticket ticket = Ticket.Create(
            Guid.NewGuid(),
            "SLA duration test",
            "The selected priority controls the resolution duration.",
            policy,
            createdAtUtc,
            priority);

        Assert.Equal(
            createdAtUtc.AddMinutes(expectedDurationMinutes),
            ticket.SlaDeadlineUtc);
    }

    /// <summary>
    /// Verifies that an urgent Ticket receives a deadline exactly 24 hours
    /// after its server-owned creation time.
    /// </summary>
    [Fact]
    public void Create_should_calculate_urgent_sla_deadline()
    {
        DateTime createdAtUtc = new(
            2026,
            9,
            11,
            12,
            0,
            0,
            DateTimeKind.Utc);

        SlaPolicy policy = SlaPolicy.Create(
            Guid.NewGuid(),
            TicketPriority.Urgent,
            resolutionDurationMinutes: 24 * 60);

        Ticket ticket = Ticket.Create(
            Guid.NewGuid(),
            "Production database is unavailable",
            "Customer operations cannot continue.",
            policy,
            createdAtUtc,
            TicketPriority.Urgent);

        Assert.Equal(policy.Id, ticket.SlaPolicyId);
        Assert.Equal(createdAtUtc, ticket.CreatedAtUtc);
        Assert.Equal(createdAtUtc.AddHours(24), ticket.SlaDeadlineUtc);
    }

    /// <summary>
    /// Verifies that an unresolved Ticket is breached only after its deadline.
    /// </summary>
    [Fact]
    public void IsSlaBreachedAt_should_use_observation_time_for_active_ticket()
    {
        DateTime createdAtUtc = new(
            2026,
            9,
            11,
            12,
            0,
            0,
            DateTimeKind.Utc);

        SlaPolicy policy = SlaPolicy.Create(
            Guid.NewGuid(),
            TicketPriority.Urgent,
            resolutionDurationMinutes: 24 * 60);

        Ticket ticket = Ticket.Create(
            Guid.NewGuid(),
            "Production database is unavailable",
            "Customer operations cannot continue.",
            policy,
            createdAtUtc,
            TicketPriority.Urgent);

        Assert.False(ticket.IsSlaBreachedAt(ticket.SlaDeadlineUtc));
        Assert.True(
            ticket.IsSlaBreachedAt(
                ticket.SlaDeadlineUtc.AddTicks(1)));
    }

    /// <summary>
    /// Verifies a later policy applies only to a newly created Ticket.
    /// </summary>
    [Fact]
    public void Later_policy_should_affect_only_new_ticket_deadline()
    {
        DateTime createdAtUtc = new(
            2026,
            9,
            11,
            12,
            0,
            0,
            DateTimeKind.Utc);

        SlaPolicy originalPolicy = SlaPolicy.Create(
            Guid.NewGuid(),
            TicketPriority.High,
            resolutionDurationMinutes: 2 * 24 * 60);

        Ticket existingTicket = Ticket.Create(
            Guid.NewGuid(),
            "Payroll export failed",
            "The payroll export cannot be generated.",
            originalPolicy,
            createdAtUtc,
            TicketPriority.High);

        SlaPolicy laterPolicy = SlaPolicy.Create(
            Guid.NewGuid(),
            TicketPriority.High,
            resolutionDurationMinutes: 24 * 60);
        Ticket newTicket = Ticket.Create(
            Guid.NewGuid(),
            "Another payroll export failed",
            "A new Ticket uses the replacement policy duration.",
            laterPolicy,
            createdAtUtc,
            TicketPriority.High);

        Assert.Equal(originalPolicy.Id, existingTicket.SlaPolicyId);
        Assert.Equal(createdAtUtc.AddDays(2), existingTicket.SlaDeadlineUtc);
        Assert.Equal(laterPolicy.Id, newTicket.SlaPolicyId);
        Assert.Equal(createdAtUtc.AddDays(1), newTicket.SlaDeadlineUtc);
    }

    /// <summary>
    /// Verifies resolution timing, rather than a later read, decides breach.
    /// </summary>
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public void Resolved_ticket_should_use_resolution_time_for_breach(
        long ticksAfterDeadline,
        bool expectedBreach)
    {
        DateTime createdAtUtc = new(
            2026,
            9,
            11,
            12,
            0,
            0,
            DateTimeKind.Utc);

        SlaPolicy policy = SlaPolicy.Create(
            Guid.NewGuid(),
            TicketPriority.Urgent,
            resolutionDurationMinutes: 24 * 60);

        Ticket ticket = Ticket.Create(
            Guid.NewGuid(),
            "Production database is unavailable",
            "Customer operations cannot continue.",
            policy,
            createdAtUtc,
            TicketPriority.Urgent);

        ticket.ChangeStatus(
            TicketStatus.InProgress,
            createdAtUtc.AddHours(1));
        ticket.ChangeStatus(
            TicketStatus.Resolved,
            ticket.SlaDeadlineUtc.AddTicks(ticksAfterDeadline));

        bool isBreached = ticket.IsSlaBreachedAt(
            ticket.SlaDeadlineUtc.AddDays(30));

        Assert.Equal(expectedBreach, isBreached);
    }

    /// <summary>
    /// Verifies reopening resumes evaluation against the original deadline.
    /// </summary>
    [Fact]
    public void Reopen_should_keep_original_deadline()
    {
        Guid requesterId = Guid.NewGuid();
        DateTime createdAtUtc = new(
            2026,
            9,
            11,
            12,
            0,
            0,
            DateTimeKind.Utc);

        SlaPolicy policy = SlaPolicy.Create(
            Guid.NewGuid(),
            TicketPriority.Urgent,
            resolutionDurationMinutes: 24 * 60);

        Ticket ticket = Ticket.Create(
            requesterId,
            "Production database is unavailable",
            "Customer operations cannot continue.",
            policy,
            createdAtUtc,
            TicketPriority.Urgent);

        DateTime originalDeadline = ticket.SlaDeadlineUtc;
        ticket.ChangeStatus(
            TicketStatus.InProgress,
            createdAtUtc.AddHours(1));
        ticket.ChangeStatus(
            TicketStatus.Resolved,
            createdAtUtc.AddHours(2));

        ticket.Reopen(
            requesterId,
            "The problem continues.",
            originalDeadline.AddHours(1));

        Assert.Equal(originalDeadline, ticket.SlaDeadlineUtc);
        Assert.True(
            ticket.IsSlaBreachedAt(originalDeadline.AddHours(1)));
    }

    /// <summary>
    /// Verifies waiting for the Customer does not pause or extend the SLA clock.
    /// </summary>
    [Fact]
    public void Waiting_customer_should_keep_sla_clock_running()
    {
        DateTime createdAtUtc = new(
            2026,
            9,
            11,
            12,
            0,
            0,
            DateTimeKind.Utc);
        SlaPolicy policy = SlaPolicy.Create(
            Guid.NewGuid(),
            TicketPriority.Urgent,
            resolutionDurationMinutes: 24 * 60);
        Ticket ticket = Ticket.Create(
            Guid.NewGuid(),
            "Customer confirmation is required",
            "Support needs one more detail before resolving the request.",
            policy,
            createdAtUtc,
            TicketPriority.Urgent);
        DateTime originalDeadline = ticket.SlaDeadlineUtc;

        ticket.ChangeStatus(
            TicketStatus.InProgress,
            createdAtUtc.AddHours(1));
        ticket.ChangeStatus(
            TicketStatus.WaitingCustomer,
            createdAtUtc.AddHours(2));

        Assert.Equal(originalDeadline, ticket.SlaDeadlineUtc);
        Assert.True(
            ticket.IsSlaBreachedAt(originalDeadline.AddTicks(1)));
    }
}
