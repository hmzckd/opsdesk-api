using OpsDesk.Domain.Enums;

namespace OpsDesk.Application.Audit.Queries;

/// <summary>
/// Holds validated filters before they cross into PostgreSQL querying.
/// </summary>
public sealed record AuditLogQuery(
    int Page,
    int PageSize,
    AuditAction? Action,
    Guid? ActorId,
    AuditTargetType? TargetType,
    Guid? TargetId,
    DateTime? FromUtc,
    DateTime? ToUtc);
