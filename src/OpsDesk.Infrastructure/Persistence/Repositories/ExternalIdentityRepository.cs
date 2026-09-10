using Microsoft.EntityFrameworkCore;
using Npgsql;
using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Application.Common.Exceptions;
using OpsDesk.Domain.Entities;

namespace OpsDesk.Infrastructure.Persistence.Repositories;

public sealed class ExternalIdentityRepository(OpsDeskDbContext database, TimeProvider timeProvider) : IExternalIdentityRepository
{
    // Joins by the stable provider key, never by email, and keeps the returned user read-only.
    public Task<User?> GetUserAsync(string issuer, string subject, CancellationToken cancellationToken = default)
    {
        return database.UserExternalIdentities.Where(identity => identity.Issuer == issuer && identity.Subject == subject)
            .Join(database.Users, identity => identity.UserId, user => user.Id, (_, user) => user)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
    }

    // Rechecks expiry after locking the invitation, then commits user, link and consumption together.
    public async Task<bool> TryAcceptInvitationAsync(Guid invitationId, User user, UserExternalIdentity identity,
        CancellationToken cancellationToken = default)
    {
        if (identity.UserId != user.Id) throw new ArgumentException("External identity user does not match.");
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        List<UserInvitation> invitations = await database.UserInvitations.FromSqlInterpolated(
            $"SELECT * FROM user_invitations WHERE id = {invitationId} FOR UPDATE")
            .AsNoTracking().ToListAsync(cancellationToken);
        UserInvitation? invitation = invitations.SingleOrDefault();
        DateTime nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        if (invitation is null || invitation.Email != user.Email || invitation.Role != user.Role || !invitation.CanAcceptAt(nowUtc))
            return false;
        database.Users.Add(user);
        database.UserExternalIdentities.Add(identity);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await database.UserInvitations.Where(row => row.Id == invitationId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.AcceptedAtUtc, nowUtc)
                    .SetProperty(row => row.AcceptedUserId, user.Id), cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "ux_users_email" or "ux_user_external_identities_issuer_subject"
        })
        {
            throw new ConflictException("An account or external identity already exists. Automatic account linking is not allowed.", exception);
        }
    }
}
