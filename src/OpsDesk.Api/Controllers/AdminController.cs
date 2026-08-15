using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsDesk.Application.Authorization;

namespace OpsDesk.Api.Controllers;

[ApiController]
[Route("admin")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class AdminController : ControllerBase
{
    [HttpGet("access")]
    public IActionResult CheckAccess()
    {
        return Ok(new
        {
            message = "Admin access granted.",
            policy = AuthorizationPolicies.AdminOnly
        });
    }
}