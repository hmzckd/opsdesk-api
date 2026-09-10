using OpsDesk.Domain.Entities;

namespace OpsDesk.Application.Invitations.Interfaces;

public interface IInvitationRepository
{
    // Reads invitation metadata without tracking changes; acceptance rechecks state atomically.
    Task<UserInvitation?> GetByTokenHashAsync(string tokenHash,
        CancellationToken cancellationToken = default);

    // Atomically consumes an active invitation and inserts its new User; false means it is no longer valid.
    Task<bool> TryAcceptAsync(Guid invitationId, User user, DateTime acceptedAtUtc,
        CancellationToken cancellationToken = default);

    // Atomically retires expired invitations and persists a new pending invitation.
    Task AddAsync(UserInvitation invitation, CancellationToken cancellationToken = default);

    // Retires an undelivered invitation so the admin can retry safely.
    Task RevokeAsync(Guid invitationId, DateTime revokedAtUtc,
        CancellationToken cancellationToken = default);
}
