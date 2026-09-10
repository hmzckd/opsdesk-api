using Microsoft.EntityFrameworkCore;
using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Domain.Entities;

namespace OpsDesk.Infrastructure.Persistence.Repositories;

public sealed class PasswordResetTokenRepository(OpsDeskDbContext database, TimeProvider timeProvider) : IPasswordResetTokenRepository
{
    // Avoids tracking during the preliminary check before expensive password hashing.
    public Task<PasswordResetToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        return database.PasswordResetTokens.AsNoTracking()
            .SingleOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);
    }

    // Uses the same account-first lock order as issuance; competing resets re-read the consumed state.
    public async Task<bool> TryResetAsync(string tokenHash, string newPasswordHash,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        Guid? userId = await database.PasswordResetTokens.Where(token => token.TokenHash == tokenHash)
            .Select(token => (Guid?)token.UserId).SingleOrDefaultAsync(cancellationToken);
        if (userId is null) return false;
        List<User> users = await database.Users.FromSqlInterpolated(
            $"SELECT * FROM users WHERE id = {userId.Value} FOR UPDATE").ToListAsync(cancellationToken);
        User? user = users.SingleOrDefault();
        if (user is null) return false;
        PasswordResetToken? token = await database.PasswordResetTokens
            .SingleOrDefaultAsync(row => row.TokenHash == tokenHash && row.UserId == user.Id, cancellationToken);
        DateTime nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        if (token is null || !token.CanUseAt(nowUtc)) return false;

        user.ResetPassword(newPasswordHash);
        token.Consume(nowUtc);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    // Locks the account, not only the token row, so first-time concurrent requests also serialize.
    public async Task<bool> TryReplaceAsync(PasswordResetToken token,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        List<User> users = await database.Users.FromSqlInterpolated(
            $"SELECT * FROM users WHERE id = {token.UserId} FOR UPDATE")
            .AsNoTracking().ToListAsync(cancellationToken);
        if (users.Count == 0) return false;
        DateTime cooldownBoundary = token.CreatedAtUtc.AddSeconds(-60);
        if (await database.PasswordResetTokens.AnyAsync(existing => existing.UserId == token.UserId
            && existing.CreatedAtUtc > cooldownBoundary, cancellationToken)) return false;

        await database.PasswordResetTokens.Where(existing => existing.UserId == token.UserId)
            .ExecuteDeleteAsync(cancellationToken);
        database.PasswordResetTokens.Add(token);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    // Shares the account-first lock with reset and issuance, so revocation cannot race with consumption.
    public async Task RevokeAsync(Guid tokenId, DateTime revokedAtUtc, CancellationToken cancellationToken = default)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        Guid? userId = await database.PasswordResetTokens.Where(token => token.Id == tokenId)
            .Select(token => (Guid?)token.UserId).SingleOrDefaultAsync(cancellationToken);
        if (userId is null) return;
        List<User> users = await database.Users.FromSqlInterpolated(
            $"SELECT * FROM users WHERE id = {userId.Value} FOR UPDATE")
            .AsNoTracking().ToListAsync(cancellationToken);
        if (users.Count == 0) return;
        await database.PasswordResetTokens.Where(token => token.Id == tokenId && token.RevokedAtUtc == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.RevokedAtUtc, revokedAtUtc),
                cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
