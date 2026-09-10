using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using OpsDesk.Application.Auth.Interfaces;

namespace OpsDesk.Api.Authorization;

public sealed class VerifiedEmailHandler(IUserRepository users)
    : AuthorizationHandler<VerifiedEmailRequirement>
{
    // Reads current account state so verification takes effect without replacing JWTs.
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        VerifiedEmailRequirement requirement)
    {
        if (!Guid.TryParse(
                context.User.FindFirstValue(ClaimTypes.NameIdentifier),
                out Guid userId))
        {
            return;
        }

        CancellationToken cancellationToken = context.Resource is HttpContext http
            ? http.RequestAborted
            : CancellationToken.None;
        var user = await users.GetByIdAsync(userId, cancellationToken);
        if (user is { IsEmailVerified: true })
        {
            context.Succeed(requirement);
        }
    }
}
