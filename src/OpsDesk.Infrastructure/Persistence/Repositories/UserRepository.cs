using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Domain.Entities;

namespace OpsDesk.Infrastructure.Persistence.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly OpsDeskDbContext _dbContext;

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

    public async Task AddAsync(
        User user,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.Users.AddAsync(
            user,
            cancellationToken);

        await _dbContext.SaveChangesAsync(
            cancellationToken);
    }
}