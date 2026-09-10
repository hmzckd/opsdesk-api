using Microsoft.Extensions.DependencyInjection;
using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Application.Auth.Models;
using OpsDesk.Domain.Entities;

namespace OpsDesk.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
public sealed class EmailVerificationTokenRepositoryTests
{
    private readonly OpsDeskApiFactory _factory;

    public EmailVerificationTokenRepositoryTests(
        OpsDeskApiFixture fixture)
    {
        _factory = fixture.Factory;
    }

    [Fact]
    public async Task Try_replace_should_persist_and_find_active_token()
    {
        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();

        IUserRepository userRepository =
            scope.ServiceProvider
                .GetRequiredService<IUserRepository>();

        IEmailVerificationTokenRepository tokenRepository =
            scope.ServiceProvider
                .GetRequiredService<
                    IEmailVerificationTokenRepository>();

        User user = CreateUser();
        await userRepository.AddAsync(user);

        EmailVerificationToken token =
            CreateToken(user.Id, 'A', DateTime.UtcNow);

        EmailVerificationTokenReplacement replacement =
            await tokenRepository.TryReplaceAsync(token, TimeSpan.Zero);

        EmailVerificationToken? persistedToken =
            await tokenRepository.GetByUserIdAsync(user.Id);

        Assert.True(replacement.Replaced);
        Assert.NotNull(persistedToken);
        Assert.Equal(token.Id, persistedToken.Id);
        Assert.Equal(user.Id, persistedToken.UserId);
    }

    [Fact]
    public async Task Replace_should_invalidate_previous_token()
    {
        Guid userId;
        DateTime createdAtUtc = DateTime.UtcNow;
        string firstTokenHash = new('B', 64);
        string secondTokenHash = new('C', 64);
        Guid secondTokenId;

        await using (AsyncServiceScope firstScope =
            _factory.Services.CreateAsyncScope())
        {
            IUserRepository userRepository =
                firstScope.ServiceProvider
                    .GetRequiredService<IUserRepository>();

            IEmailVerificationTokenRepository tokenRepository =
                firstScope.ServiceProvider
                    .GetRequiredService<
                        IEmailVerificationTokenRepository>();

            User user = CreateUser();
            await userRepository.AddAsync(user);
            userId = user.Id;

            EmailVerificationToken first =
                CreateToken(userId, 'B', createdAtUtc);

            await tokenRepository.TryReplaceAsync(
                first,
                TimeSpan.Zero);
        }

        await using (AsyncServiceScope secondScope =
            _factory.Services.CreateAsyncScope())
        {
            IEmailVerificationTokenRepository tokenRepository =
                secondScope.ServiceProvider
                    .GetRequiredService<
                        IEmailVerificationTokenRepository>();

            EmailVerificationToken second =
                CreateToken(
                    userId,
                    'C',
                    createdAtUtc.AddMinutes(1));
            secondTokenId = second.Id;

            await tokenRepository.TryReplaceAsync(
                second,
                TimeSpan.Zero);
        }

        await using AsyncServiceScope verificationScope =
            _factory.Services.CreateAsyncScope();

        IEmailVerificationTokenRepository verificationRepository =
            verificationScope.ServiceProvider
                .GetRequiredService<
                    IEmailVerificationTokenRepository>();

        EmailVerificationToken? oldToken =
            await verificationRepository.GetByHashAsync(
                firstTokenHash);
        EmailVerificationToken? activeToken =
            await verificationRepository.GetByHashAsync(
                secondTokenHash);

        Assert.Null(oldToken);
        Assert.NotNull(activeToken);
        Assert.Equal(secondTokenId, activeToken.Id);
    }

    [Fact]
    public async Task Get_by_user_id_should_return_active_token()
    {
        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();

        IUserRepository userRepository =
            scope.ServiceProvider
                .GetRequiredService<IUserRepository>();

        IEmailVerificationTokenRepository tokenRepository =
            scope.ServiceProvider
                .GetRequiredService<
                    IEmailVerificationTokenRepository>();

        User user = CreateUser();
        await userRepository.AddAsync(user);

        EmailVerificationToken token =
            CreateToken(user.Id, 'E', DateTime.UtcNow);

        await tokenRepository.TryReplaceAsync(token, TimeSpan.Zero);

        EmailVerificationToken? activeToken =
            await tokenRepository.GetByUserIdAsync(user.Id);

        Assert.NotNull(activeToken);
        Assert.Equal(token.Id, activeToken.Id);
        Assert.Equal(token.TokenHash, activeToken.TokenHash);
    }

    [Fact]
    public async Task Try_confirm_should_verify_user_and_consume_token()
    {
        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();

        IUserRepository userRepository =
            scope.ServiceProvider
                .GetRequiredService<IUserRepository>();

        IEmailVerificationTokenRepository tokenRepository =
            scope.ServiceProvider
                .GetRequiredService<
                    IEmailVerificationTokenRepository>();

        User user = CreateUser();
        await userRepository.AddAsync(user);

        EmailVerificationToken token =
            CreateToken(user.Id, 'D', DateTime.UtcNow);

        await tokenRepository.TryReplaceAsync(token, TimeSpan.Zero);
        bool confirmed = await tokenRepository.TryConfirmAsync(
            token.TokenHash,
            user,
            DateTime.UtcNow);

        EmailVerificationToken? consumedToken =
            await tokenRepository.GetByHashAsync(
                token.TokenHash);

        Assert.True(confirmed);
        Assert.True(user.IsEmailVerified);
        Assert.Null(consumedToken);
    }

    [Fact]
    public async Task Concurrent_confirmation_should_allow_only_one_winner()
    {
        Guid userId;
        string tokenHash = new('F', 64);
        DateTime nowUtc = DateTime.UtcNow;

        await using (AsyncServiceScope setupScope =
            _factory.Services.CreateAsyncScope())
        {
            IUserRepository users = setupScope.ServiceProvider
                .GetRequiredService<IUserRepository>();
            IEmailVerificationTokenRepository tokens = setupScope.ServiceProvider
                .GetRequiredService<IEmailVerificationTokenRepository>();
            User user = CreateUser();
            await users.AddAsync(user);
            userId = user.Id;
            await tokens.TryReplaceAsync(
                CreateToken(userId, 'F', nowUtc),
                TimeSpan.Zero);
        }

        async Task<bool> ConfirmFromIndependentRequestAsync()
        {
            await using AsyncServiceScope scope =
                _factory.Services.CreateAsyncScope();
            IUserRepository users = scope.ServiceProvider
                .GetRequiredService<IUserRepository>();
            IEmailVerificationTokenRepository tokens = scope.ServiceProvider
                .GetRequiredService<IEmailVerificationTokenRepository>();
            User user = await users.GetByIdAsync(userId)
                ?? throw new InvalidOperationException("User was not found.");
            return await tokens.TryConfirmAsync(
                tokenHash,
                user,
                nowUtc.AddMinutes(1));
        }

        bool[] results = await Task.WhenAll(
            ConfirmFromIndependentRequestAsync(),
            ConfirmFromIndependentRequestAsync());

        Assert.Single(results, result => result);
        Assert.Single(results, result => !result);
    }

    private static User CreateUser()
    {
        return new User
        {
            FirstName = "Email",
            LastName = "Verification",
            Email = $"verification-{Guid.NewGuid():N}@example.com",
            PasswordHash = "not-a-real-password-hash"
        };
    }

    private static EmailVerificationToken CreateToken(
        Guid userId,
        char hashCharacter,
        DateTime createdAtUtc)
    {
        return EmailVerificationToken.Create(
            userId,
            new string(hashCharacter, 64),
            createdAtUtc,
            createdAtUtc.AddHours(8));
    }
}
