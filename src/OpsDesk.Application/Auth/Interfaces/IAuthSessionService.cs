namespace OpsDesk.Application.Auth.Interfaces;

public interface IAuthSessionService
{
    // Revokes every currently issued OpsDesk JWT for one local account.
    Task RevokeAllAsync(Guid userId, CancellationToken cancellationToken = default);
}
