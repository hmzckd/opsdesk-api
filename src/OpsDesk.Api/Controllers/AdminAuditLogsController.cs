using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsDesk.Application.Audit.DTOs;
using OpsDesk.Application.Audit.Interfaces;
using OpsDesk.Application.Authorization;
using OpsDesk.Application.Common.Pagination;

namespace OpsDesk.Api.Controllers;

[ApiController]
[Route("admin/audit-logs")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class AdminAuditLogsController(IAuditLogService auditLogs)
    : ControllerBase
{
    /// <summary>
    /// Returns one filtered, newest-first page of safe audit entries.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<PagedResponse<AuditLogResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResponse<AuditLogResponse>>> List(
        [FromQuery] ListAuditLogsRequest request,
        CancellationToken cancellationToken)
    {
        PagedResponse<AuditLogResponse> response = await auditLogs.ListAsync(
            request, cancellationToken);
        return Ok(response);
    }
}
