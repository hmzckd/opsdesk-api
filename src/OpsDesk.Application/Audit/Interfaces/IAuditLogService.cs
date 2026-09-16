using OpsDesk.Application.Audit.DTOs;
using OpsDesk.Application.Common.Pagination;

namespace OpsDesk.Application.Audit.Interfaces;

public interface IAuditLogService
{
    /// <summary>
    /// Validates Admin query values and returns a bounded audit page.
    /// </summary>
    Task<PagedResponse<AuditLogResponse>> ListAsync(
        ListAuditLogsRequest request,
        CancellationToken cancellationToken = default);
}
