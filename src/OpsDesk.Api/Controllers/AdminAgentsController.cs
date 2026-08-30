using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsDesk.Application.Agents.DTOs;
using OpsDesk.Application.Agents.Interfaces;
using OpsDesk.Application.Authorization;

namespace OpsDesk.Api.Controllers;

[ApiController]
[Route("admin/agents")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class AdminAgentsController : ControllerBase
{
    private readonly IAgentService _agentService;

    public AdminAgentsController(IAgentService agentService)
    {
        _agentService = agentService;
    }

    /// <summary>
    /// Provisions an Agent account that can authenticate through /auth/login.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<AgentResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AgentResponse>> Create(
        CreateAgentRequest request,
        CancellationToken cancellationToken)
    {
        AgentResponse response = await _agentService.CreateAsync(
            request,
            cancellationToken);

        return StatusCode(
            StatusCodes.Status201Created,
            response);
    }
}
