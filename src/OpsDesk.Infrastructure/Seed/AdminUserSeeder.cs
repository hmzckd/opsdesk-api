using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Application.Auth.Services;
using OpsDesk.Domain.Entities;
using OpsDesk.Domain.Enums;
using OpsDesk.Infrastructure.Persistence;

namespace OpsDesk.Infrastructure.Seed;

public sealed class AdminUserSeeder
{
    private readonly OpsDeskDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IPasswordValidator _passwordValidator;
    private readonly IEmailValidator _emailValidator;
    private readonly AdminSeedSettings _settings;
    private readonly ILogger<AdminUserSeeder> _logger;

    public AdminUserSeeder(
        OpsDeskDbContext dbContext,
        IPasswordHasher passwordHasher,
        IPasswordValidator passwordValidator,
        IEmailValidator emailValidator,
        IOptions<AdminSeedSettings> options,
        ILogger<AdminUserSeeder> logger)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _passwordValidator = passwordValidator;
        _emailValidator = emailValidator;
        _settings = options.Value;
        _logger = logger;
    }

    public async Task SeedAsync(
        CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
        {
            return;
        }

        string firstName = UserInputNormalizer.NormalizeName(
            _settings.FirstName,
            nameof(_settings.FirstName));

        string lastName = UserInputNormalizer.NormalizeName(
            _settings.LastName,
            nameof(_settings.LastName));

        _emailValidator.Validate(_settings.Email);

        string normalizedEmail =
            UserInputNormalizer.NormalizeEmail(_settings.Email);

        User? existingUser = await _dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(
                user => user.Email == normalizedEmail,
                cancellationToken);

        if (existingUser is not null)
        {
            if (existingUser.Role != UserRole.Admin)
            {
                throw new InvalidOperationException(
                    $"The admin seed email '{normalizedEmail}' " +
                    "belongs to a non-admin user.");
            }

            _logger.LogInformation(
                "Admin seed skipped because {Email} already exists.",
                normalizedEmail);

            return;
        }

        _passwordValidator.Validate(_settings.Password);

        var admin = new User
        {
            FirstName = firstName,
            LastName = lastName,
            Email = normalizedEmail,
            PasswordHash = _passwordHasher.HashPassword(
                _settings.Password),
            Role = UserRole.Admin
        };

        await _dbContext.Users.AddAsync(
            admin,
            cancellationToken);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        _logger.LogInformation(
            "Admin user {Email} was created.",
            normalizedEmail);
    }
}
