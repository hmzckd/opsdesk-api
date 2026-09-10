using System.Threading;
using System.Threading.Tasks;
using OpsDesk.Domain.Entities;

namespace OpsDesk.Application.Auth.Interfaces;

public interface IUserRepository
{
    // Reads only the server-side session version; null means the account no longer exists.
    Task<int?> GetAuthVersionAsync(Guid userId, CancellationToken cancellationToken = default);

    // Atomically advances the version so all previously issued JWTs fail validation.
    Task<bool> RevokeSessionsAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<bool> ExistsByEmailAsync(
        string email,
        CancellationToken cancellationToken = default);

    Task<User?> GetByEmailAsync(
        string email,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves one User by identity without tracking it for changes.
    /// </summary>
    Task<User?> GetByIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves one User with tracking enabled for an update.
    /// </summary>
    Task<User?> GetByIdForUpdateAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        User user,
        CancellationToken cancellationToken = default);
}
