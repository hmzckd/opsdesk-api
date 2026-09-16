using OpsDesk.Application.Audit.DTOs;
using OpsDesk.Application.Audit.Queries;
using OpsDesk.Application.Common.Pagination;
using OpsDesk.Domain.Entities;

namespace OpsDesk.Application.Audit.Interfaces;

public interface IAuditLogRepository
{
    /// <summary>
    /// Stages an audit entry for the caller's next database save.
    /// </summary>
    void Stage(AuditLog entry);

    /// <summary>
    /// Reads one filtered page without exposing EF Core to Application.
    /// </summary>
    Task<PagedResponse<AuditLogResponse>> GetPageAsync(
        AuditLogQuery query,
        CancellationToken cancellationToken = default);
}
