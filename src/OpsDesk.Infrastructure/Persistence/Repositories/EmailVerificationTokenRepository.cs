using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Application.Auth.Models;
using OpsDesk.Domain.Entities;

namespace OpsDesk.Infrastructure.Persistence.Repositories;

public sealed class EmailVerificationTokenRepository :
    IEmailVerificationTokenRepository
{
    private readonly OpsDeskDbContext _dbContext;

    public EmailVerificationTokenRepository(
        OpsDeskDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Retrieves the current token without EF change-tracking overhead.
    /// </summary>
    public Task<EmailVerificationToken?> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.EmailVerificationTokens
            .AsNoTracking()
            .SingleOrDefaultAsync(
                token => token.UserId == userId,
                cancellationToken);
    }

    /// <summary>
    /// Retrieves a token without EF change tracking for eligibility checks.
    /// </summary>
    public Task<EmailVerificationToken?> GetByHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        return _dbContext.EmailVerificationTokens
            .AsNoTracking()
            .SingleOrDefaultAsync(
                token => token.TokenHash == tokenHash,
                cancellationToken);
    }

    /// <summary>
    /// Locks the User so concurrent requests recheck the cooldown one at a time.
    /// </summary>
    public async Task<EmailVerificationTokenReplacement> TryReplaceAsync(
        EmailVerificationToken token,
        TimeSpan cooldown,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(token);

        await using IDbContextTransaction transaction =
            await _dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        List<User> users = await _dbContext.Users
            .FromSqlInterpolated(
                $"SELECT * FROM users WHERE id = {token.UserId} FOR UPDATE")
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        if (users.Count == 0)
        {
            return new EmailVerificationTokenReplacement(false, null);
        }

        EmailVerificationToken? previousToken =
            await _dbContext.EmailVerificationTokens
                .SingleOrDefaultAsync(
                    existing => existing.UserId == token.UserId,
                    cancellationToken);

        if (previousToken is not null &&
            token.CreatedAtUtc < previousToken.CreatedAtUtc.Add(cooldown))
        {
            return new EmailVerificationTokenReplacement(
                false,
                previousToken);
        }

        if (previousToken is not null)
        {
            _dbContext.EmailVerificationTokens.Remove(previousToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        await _dbContext.EmailVerificationTokens.AddAsync(
            token,
            cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new EmailVerificationTokenReplacement(
            true,
            previousToken);
    }

    /// <summary>
    /// Rolls back the logical token replacement when SMTP delivery fails.
    /// </summary>
    public async Task RestoreAsync(
        Guid replacementTokenId,
        EmailVerificationToken? previousToken,
        CancellationToken cancellationToken = default)
    {
        await using IDbContextTransaction transaction =
            await _dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        Guid? userId = await _dbContext.EmailVerificationTokens
            .Where(token => token.Id == replacementTokenId)
            .Select(token => (Guid?)token.UserId)
            .SingleOrDefaultAsync(cancellationToken);
        if (userId is null)
        {
            return;
        }

        await _dbContext.Users
            .FromSqlInterpolated(
                $"SELECT * FROM users WHERE id = {userId.Value} FOR UPDATE")
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        EmailVerificationToken? activeToken =
            await _dbContext.EmailVerificationTokens
                .SingleOrDefaultAsync(
                    token => token.UserId == userId.Value,
                    cancellationToken);
        if (activeToken?.Id != replacementTokenId)
        {
            return;
        }

        _dbContext.EmailVerificationTokens.Remove(activeToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (previousToken is not null &&
            previousToken.UserId == userId.Value)
        {
            _dbContext.Entry(previousToken).State =
                EntityState.Detached;
            await _dbContext.EmailVerificationTokens.AddAsync(
                previousToken,
                cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// Verifies the User and consumes the matching unexpired token atomically.
    /// </summary>
    public async Task<bool> TryConfirmAsync(
        string tokenHash,
        User user,
        DateTime verifiedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        ArgumentNullException.ThrowIfNull(user);

        await using IDbContextTransaction transaction =
            await _dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        List<EmailVerificationToken> tokens =
            await _dbContext.EmailVerificationTokens
                .FromSqlInterpolated(
                    $"SELECT * FROM email_verification_tokens WHERE token_hash = {tokenHash} FOR UPDATE")
                .ToListAsync(cancellationToken);
        EmailVerificationToken? token = tokens.SingleOrDefault();
        if (token is null || token.UserId != user.Id ||
            token.IsExpiredAt(verifiedAtUtc))
        {
            return false;
        }

        List<User> users = await _dbContext.Users
            .FromSqlInterpolated(
                $"SELECT * FROM users WHERE id = {user.Id} FOR UPDATE")
            .ToListAsync(cancellationToken);
        User? lockedUser = users.SingleOrDefault();
        if (lockedUser is null)
        {
            return false;
        }

        lockedUser.MarkEmailVerified(verifiedAtUtc);
        _dbContext.EmailVerificationTokens.Remove(token);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
