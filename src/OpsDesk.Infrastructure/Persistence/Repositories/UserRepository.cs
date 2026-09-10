using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Application.Common.Exceptions;
using OpsDesk.Domain.Entities;

namespace OpsDesk.Infrastructure.Persistence.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly OpsDeskDbContext _dbContext;

    // Projects one scalar for JWT validation without loading the account's password hash.
    public Task<int?> GetAuthVersionAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Users.Where(user => user.Id == userId)
            .Select(user => (int?)user.AuthVersion).SingleOrDefaultAsync(cancellationToken);
    }

    // Uses one SQL update so concurrent logout/reset operations cannot overwrite each other.
    public async Task<bool> RevokeSessionsAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        int affectedRows = await _dbContext.Users
            .Where(user => user.Id == userId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(user => user.AuthVersion, user => user.AuthVersion + 1), cancellationToken);
        return affectedRows == 1;
    }

    public UserRepository(OpsDeskDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<bool> ExistsByEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Users.AnyAsync(
            user => user.Email == email,
            cancellationToken);
    }

    public Task<User?> GetByEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(
                user => user.Email == email,
                cancellationToken);
    }

    /// <summary>
    /// Retrieves one User as read-only data for identity and role checks.
    /// </summary>
    public Task<User?> GetByIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(
                user => user.Id == userId,
                cancellationToken);
    }

    /// <summary>
    /// Retrieves one User with EF tracking so its state can be persisted.
    /// </summary>
    public Task<User?> GetByIdForUpdateAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Users.SingleOrDefaultAsync(
            user => user.Id == userId,
            cancellationToken);
    }

    public async Task AddAsync(
        User user,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.Users.AddAsync(
            user,
            cancellationToken);

        try
        {
            await _dbContext.SaveChangesAsync(
                cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "ux_users_email"
            })
        {
            throw new ConflictException(
                "A user with this email address already exists.",
                exception);
        }
    }
}
