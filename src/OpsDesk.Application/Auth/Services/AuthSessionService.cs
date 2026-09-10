using OpsDesk.Application.Auth.Interfaces;

namespace OpsDesk.Application.Auth.Services;

public sealed class AuthSessionService(IUserRepository users) : IAuthSessionService
{
    // Advances the server-side version checked on every authenticated API request.
    public async Task RevokeAllAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || !await users.RevokeSessionsAsync(userId, cancellationToken))
        {
            throw new UnauthorizedAccessException("The authenticated account no longer exists.");
        }
    }
}
