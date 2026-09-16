using Microsoft.EntityFrameworkCore;
using Npgsql;
using OpsDesk.Application.Common.Exceptions;
using OpsDesk.Application.Invitations.Interfaces;
using OpsDesk.Domain.Entities;

namespace OpsDesk.Infrastructure.Persistence.Repositories;

public sealed class InvitationRepository(
    OpsDeskDbContext database,
    TimeProvider timeProvider) : IInvitationRepository
{
    // Loads a read-only invitation; the write path must recheck its state under a database lock.
    public Task<UserInvitation?> GetByTokenHashAsync(string tokenHash,
        CancellationToken cancellationToken = default)
    {
        return database.UserInvitations.AsNoTracking()
            .SingleOrDefaultAsync(invitation => invitation.TokenHash == tokenHash, cancellationToken);
    }

    // The conditional update locks the row; account creation and acceptance commit or roll back together.
    public async Task<bool> TryAcceptAsync(Guid invitationId, User user, DateTime acceptedAtUtc,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        List<UserInvitation> lockedInvitations = await database.UserInvitations
            .FromSqlInterpolated(
                $"SELECT * FROM user_invitations WHERE id = {invitationId} FOR UPDATE")
            .ToListAsync(cancellationToken);
        if (lockedInvitations.Count == 0)
        {
            return false;
        }

        UserInvitation? invitation = await database.UserInvitations
            .AsNoTracking()
            .SingleOrDefaultAsync(
                row => row.Id == invitationId,
                cancellationToken);
        DateTime effectiveAcceptedAtUtc =
            timeProvider.GetUtcNow().UtcDateTime;
        if (invitation is null ||
            invitation.Email != user.Email ||
            invitation.Role != user.Role ||
            !invitation.CanAcceptAt(effectiveAcceptedAtUtc))
        {
            return false;
        }

        await database.UserInvitations
            .Where(row => row.Id == invitationId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(
                row => row.AcceptedAtUtc,
                effectiveAcceptedAtUtc), cancellationToken);

        database.Users.Add(user);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await database.UserInvitations.Where(invitation => invitation.Id == invitationId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(
                    invitation => invitation.AcceptedUserId, user.Id), cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "ux_users_email"
        })
        {
            throw new ConflictException("An account with this email already exists.", exception);
        }
    }

    // Retires expired records and saves the replacement within one transaction.
    public async Task AddAsync(UserInvitation invitation, CancellationToken cancellationToken = default)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        await database.UserInvitations
            .Where(x => x.Email == invitation.Email && x.RevokedAtUtc == null && x.AcceptedAtUtc == null
                && x.ExpiresAtUtc <= invitation.CreatedAtUtc)
            .ExecuteUpdateAsync(setters => setters.SetProperty(
                x => x.RevokedAtUtc, invitation.CreatedAtUtc), cancellationToken);

        database.UserInvitations.Add(invitation);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "ux_user_invitations_pending_email"
            })
        {
            throw new ConflictException("An active invitation for this email already exists.");
        }
    }

    // A failed database write leaves the already-sent link unusable rather than activating it implicitly.
    public async Task MarkEmailSentAsync(Guid invitationId, DateTime sentAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (sentAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Email send time must be UTC.", nameof(sentAtUtc));
        }

        int updated = await database.UserInvitations
            .Where(x => x.Id == invitationId && x.EmailSentAtUtc == null
                && x.RevokedAtUtc == null && x.AcceptedAtUtc == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(
                x => x.EmailSentAtUtc, sentAtUtc), cancellationToken);
        if (updated != 1)
        {
            throw new InvalidOperationException("The invitation could not be activated after email delivery.");
        }
    }

    // Keeps an unsuccessful invitation as history while releasing its pending-email slot.
    public async Task RevokeAsync(Guid invitationId, DateTime revokedAtUtc,
        AuditLog auditLog,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(auditLog);
        await using var transaction = await database.Database
            .BeginTransactionAsync(cancellationToken);

        int revoked = await database.UserInvitations.Where(x => x.Id == invitationId
                && x.RevokedAtUtc == null && x.AcceptedAtUtc == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(
                x => x.RevokedAtUtc, revokedAtUtc), cancellationToken);

        if (revoked == 1)
        {
            database.AuditLogs.Add(auditLog);
            await database.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }
}
