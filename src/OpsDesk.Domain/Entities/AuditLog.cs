using OpsDesk.Domain.Enums;

namespace OpsDesk.Domain.Entities;

public sealed class AuditLog
{
    private AuditLog()
    {
    }

    public Guid Id { get; private set; }
    public AuditAction Action { get; private set; }
    public Guid ActorId { get; private set; }
    public AuditTargetType TargetType { get; private set; }
    public Guid TargetId { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
    public TicketStatus? PreviousStatus { get; private set; }
    public TicketStatus? NewStatus { get; private set; }
    public Guid? PreviousAssigneeId { get; private set; }
    public Guid? NewAssigneeId { get; private set; }

    /// <summary>
    /// Records a successfully created Ticket without copying its free-text content.
    /// </summary>
    public static AuditLog ForTicketCreated(
        Guid ticketId, Guid actorId, DateTime occurredAtUtc) =>
        Create(AuditAction.TicketCreated, AuditTargetType.Ticket,
            ticketId, actorId, occurredAtUtc);

    /// <summary>
    /// Records one successful Ticket status transition, including reopen.
    /// </summary>
    public static AuditLog ForTicketStatusChanged(
        Guid ticketId, Guid actorId, TicketStatus previousStatus,
        TicketStatus newStatus, DateTime occurredAtUtc)
    {
        if (previousStatus == newStatus)
        {
            throw new ArgumentException(
                "An audit status change requires different values.",
                nameof(newStatus));
        }

        AuditLog entry = Create(AuditAction.TicketStatusChanged,
            AuditTargetType.Ticket, ticketId, actorId, occurredAtUtc);
        entry.PreviousStatus = previousStatus;
        entry.NewStatus = newStatus;
        return entry;
    }

    /// <summary>
    /// Records one successful assignment or unassignment using only User IDs.
    /// </summary>
    public static AuditLog ForTicketAssigneeChanged(
        Guid ticketId, Guid actorId, Guid? previousAssigneeId,
        Guid? newAssigneeId, DateTime occurredAtUtc)
    {
        if (previousAssigneeId == newAssigneeId ||
            previousAssigneeId == Guid.Empty ||
            newAssigneeId == Guid.Empty)
        {
            throw new ArgumentException(
                "An audit assignment change requires distinct valid IDs.",
                nameof(newAssigneeId));
        }

        AuditLog entry = Create(AuditAction.TicketAssigneeChanged,
            AuditTargetType.Ticket, ticketId, actorId, occurredAtUtc);
        entry.PreviousAssigneeId = previousAssigneeId;
        entry.NewAssigneeId = newAssigneeId;
        return entry;
    }

    /// <summary>
    /// Records an Admin-created Agent without storing account credentials.
    /// </summary>
    public static AuditLog ForAgentCreated(
        Guid agentId, Guid actorId, DateTime occurredAtUtc) =>
        Create(AuditAction.AgentCreated, AuditTargetType.User,
            agentId, actorId, occurredAtUtc);

    /// <summary>
    /// Records a persisted invitation, not a claim of email delivery.
    /// </summary>
    public static AuditLog ForInvitationCreated(
        Guid invitationId, Guid actorId, DateTime occurredAtUtc) =>
        Create(AuditAction.InvitationCreated, AuditTargetType.Invitation,
            invitationId, actorId, occurredAtUtc);

    /// <summary>
    /// Records the revocation of an invitation after failed delivery.
    /// </summary>
    public static AuditLog ForInvitationRevoked(
        Guid invitationId, Guid actorId, DateTime occurredAtUtc) =>
        Create(AuditAction.InvitationRevoked, AuditTargetType.Invitation,
            invitationId, actorId, occurredAtUtc);

    private static AuditLog Create(
        AuditAction action, AuditTargetType targetType,
        Guid targetId, Guid actorId, DateTime occurredAtUtc)
    {
        if (targetId == Guid.Empty || actorId == Guid.Empty)
        {
            throw new ArgumentException(
                "Audit entries require a target and an actor.");
        }

        if (occurredAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "Audit entry time must be UTC.", nameof(occurredAtUtc));
        }

        return new AuditLog
        {
            Id = Guid.NewGuid(),
            Action = action,
            ActorId = actorId,
            TargetType = targetType,
            TargetId = targetId,
            OccurredAtUtc = occurredAtUtc
        };
    }
}
