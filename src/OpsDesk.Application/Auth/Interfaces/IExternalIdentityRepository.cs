using OpsDesk.Domain.Entities;

namespace OpsDesk.Application.Auth.Interfaces;

public interface IExternalIdentityRepository
{
    // Resolves an established link without matching or changing any account by email.
    Task<User?> GetUserAsync(string issuer, string subject, CancellationToken cancellationToken = default);

    // Creates the account/link and consumes a still-valid matching invitation in one transaction.
    Task<bool> TryAcceptInvitationAsync(Guid invitationId, User user, UserExternalIdentity identity,
        CancellationToken cancellationToken = default);
}
