using OpsDesk.Domain.Enums;

namespace OpsDesk.Application.Audit.DTOs;

/// <summary>
/// Exposes only explicitly allowlisted audit fields to an Admin.
/// </summary>
public sealed record AuditLogResponse(
    Guid Id,
    AuditAction Action,
    Guid ActorId,
    AuditTargetType TargetType,
    Guid TargetId,
    DateTime OccurredAtUtc,
    TicketStatus? PreviousStatus,
    TicketStatus? NewStatus,
    Guid? PreviousAssigneeId,
    Guid? NewAssigneeId);
