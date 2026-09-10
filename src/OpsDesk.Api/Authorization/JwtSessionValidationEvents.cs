using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Application.Authorization;

namespace OpsDesk.Api.Authorization;

public sealed class JwtSessionValidationEvents(IUserRepository users) : JwtBearerEvents
{
    // Runs after signature/issuer/audience/expiry validation and rejects revoked or legacy sessions.
    public override async Task TokenValidated(TokenValidatedContext context)
    {
        string[] identifiers = context.Principal?.FindAll(ClaimTypes.NameIdentifier).Select(claim => claim.Value).ToArray() ?? [];
        string[] versions = context.Principal?.FindAll(AuthClaimTypes.AuthVersion).Select(claim => claim.Value).ToArray() ?? [];
        if (identifiers.Length != 1 || versions.Length != 1
            || !Guid.TryParse(identifiers[0], out Guid userId)
            || !int.TryParse(versions[0], NumberStyles.None, CultureInfo.InvariantCulture, out int version))
        {
            context.Fail("Session is invalid. Sign in again.");
            return;
        }
        int? currentVersion = await users.GetAuthVersionAsync(userId, context.HttpContext.RequestAborted);
        if (currentVersion is null || currentVersion != version)
            context.Fail("Session is invalid. Sign in again.");
    }
}
