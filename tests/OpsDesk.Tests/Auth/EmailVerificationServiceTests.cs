using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Application.Auth.Models;
using OpsDesk.Application.Auth.Services;
using OpsDesk.Domain.Entities;
using OpsDesk.Infrastructure.Authentication;

namespace OpsDesk.Tests.Auth;

public sealed class EmailVerificationServiceTests
{
    [Fact]
    public async Task Issue_should_store_eight_hour_token_and_send_raw_value()
    {
        var now = new DateTimeOffset(
            2026,
            9,
            4,
            12,
            0,
            0,
            TimeSpan.Zero);
        var tokenRepository =
            new InMemoryEmailVerificationTokenRepository();
        var tokenGenerator =
            new EmailVerificationTokenGenerator();
        var emailSender =
            new RecordingEmailVerificationEmailSender();
        var timeProvider = new ManualTimeProvider(now);
        var userRepository = new InMemoryUserRepository();
        var service = new EmailVerificationService(
            userRepository,
            tokenRepository,
            tokenGenerator,
            emailSender,
            new EmailValidator(),
            new InMemoryEmailVerificationQueue(),
            timeProvider);
        User user = CreateUser();

        await service.IssueAsync(user);

        EmailVerificationToken? storedToken =
            await tokenRepository.GetByUserIdAsync(user.Id);
        EmailVerificationEmail sentEmail =
            Assert.Single(emailSender.SentEmails);

        Assert.NotNull(storedToken);
        Assert.Equal(now.UtcDateTime, storedToken.CreatedAtUtc);
        Assert.Equal(
            now.AddHours(8).UtcDateTime,
            storedToken.ExpiresAtUtc);
        Assert.Equal(user.Email, sentEmail.RecipientEmail);
        Assert.Equal(
            storedToken.TokenHash,
            tokenGenerator.ComputeHash(sentEmail.RawToken));
        Assert.Equal(
            storedToken.ExpiresAtUtc,
            sentEmail.ExpiresAtUtc);
    }

    [Fact]
    public async Task Resend_before_cooldown_should_keep_existing_token()
    {
        var now = new DateTimeOffset(
            2026,
            9,
            4,
            12,
            0,
            0,
            TimeSpan.Zero);
        var userRepository = new InMemoryUserRepository();
        var tokenRepository =
            new InMemoryEmailVerificationTokenRepository();
        var emailSender =
            new RecordingEmailVerificationEmailSender();
        var timeProvider = new ManualTimeProvider(now);
        var service = new EmailVerificationService(
            userRepository,
            tokenRepository,
            new EmailVerificationTokenGenerator(),
            emailSender,
            new EmailValidator(),
            new InMemoryEmailVerificationQueue(),
            timeProvider);
        User user = CreateUser();
        await userRepository.AddAsync(user);
        await service.IssueAsync(user);
        EmailVerificationToken originalToken =
            await tokenRepository.GetByUserIdAsync(user.Id)
            ?? throw new InvalidOperationException(
                "Initial token was not stored.");

        timeProvider.Advance(TimeSpan.FromSeconds(59));
        await service.ResendAsync(user.Email);

        EmailVerificationToken activeToken =
            await tokenRepository.GetByUserIdAsync(user.Id)
            ?? throw new InvalidOperationException(
                "Active token was not found.");

        Assert.Equal(originalToken.Id, activeToken.Id);
        Assert.Single(emailSender.SentEmails);
    }

    [Fact]
    public async Task Resend_at_cooldown_boundary_should_replace_token()
    {
        var now = new DateTimeOffset(
            2026,
            9,
            4,
            12,
            0,
            0,
            TimeSpan.Zero);
        var userRepository = new InMemoryUserRepository();
        var tokenRepository =
            new InMemoryEmailVerificationTokenRepository();
        var tokenGenerator =
            new EmailVerificationTokenGenerator();
        var emailSender =
            new RecordingEmailVerificationEmailSender();
        var timeProvider = new ManualTimeProvider(now);
        var service = new EmailVerificationService(
            userRepository,
            tokenRepository,
            tokenGenerator,
            emailSender,
            new EmailValidator(),
            new InMemoryEmailVerificationQueue(),
            timeProvider);
        User user = CreateUser();
        await userRepository.AddAsync(user);
        await service.IssueAsync(user);
        EmailVerificationToken originalToken =
            await tokenRepository.GetByUserIdAsync(user.Id)
            ?? throw new InvalidOperationException(
                "Initial token was not stored.");

        timeProvider.Advance(TimeSpan.FromSeconds(60));
        await service.ResendAsync(user.Email);
        await service.SendQueuedAsync(
            user.Email,
            timeProvider.GetUtcNow().UtcDateTime);

        EmailVerificationToken activeToken =
            await tokenRepository.GetByUserIdAsync(user.Id)
            ?? throw new InvalidOperationException(
                "Replacement token was not stored.");
        EmailVerificationEmail latestEmail =
            emailSender.SentEmails[^1];

        Assert.NotEqual(originalToken.Id, activeToken.Id);
        Assert.Equal(2, emailSender.SentEmails.Count);
        Assert.Equal(
            activeToken.TokenHash,
            tokenGenerator.ComputeHash(latestEmail.RawToken));
    }

    [Fact]
    public async Task Issue_when_delivery_fails_should_preserve_existing_token()
    {
        var now = new DateTimeOffset(
            2026, 9, 4, 12, 0, 0, TimeSpan.Zero);
        var tokenRepository =
            new InMemoryEmailVerificationTokenRepository();
        var emailSender =
            new RecordingEmailVerificationEmailSender();
        var timeProvider = new ManualTimeProvider(now);
        var service = new EmailVerificationService(
            new InMemoryUserRepository(),
            tokenRepository,
            new EmailVerificationTokenGenerator(),
            emailSender,
            new EmailValidator(),
            new InMemoryEmailVerificationQueue(),
            timeProvider);
        User user = CreateUser();
        await service.IssueAsync(user);
        EmailVerificationToken originalToken =
            await tokenRepository.GetByUserIdAsync(user.Id)
            ?? throw new InvalidOperationException(
                "Initial token was not stored.");

        timeProvider.Advance(TimeSpan.FromSeconds(60));
        emailSender.FailDelivery = true;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.IssueAsync(user));

        EmailVerificationToken activeToken =
            await tokenRepository.GetByUserIdAsync(user.Id)
            ?? throw new InvalidOperationException(
                "Original token was not preserved.");
        Assert.Equal(originalToken.Id, activeToken.Id);
    }

    [Fact]
    public async Task Concurrent_resends_should_send_only_one_replacement()
    {
        var now = new DateTimeOffset(
            2026, 9, 4, 12, 0, 0, TimeSpan.Zero);
        var users = new InMemoryUserRepository();
        var tokens = new InMemoryEmailVerificationTokenRepository();
        var sender = new RecordingEmailVerificationEmailSender();
        var clock = new ManualTimeProvider(now);
        var service = new EmailVerificationService(
            users,
            tokens,
            new EmailVerificationTokenGenerator(),
            sender,
            new EmailValidator(),
            new InMemoryEmailVerificationQueue(),
            clock);
        User user = CreateUser();
        await users.AddAsync(user);
        await service.IssueAsync(user);
        clock.Advance(TimeSpan.FromSeconds(60));
        tokens.CoordinateNextTwoReplacements();

        await Task.WhenAll(
            service.SendQueuedAsync(user.Email, clock.GetUtcNow().UtcDateTime),
            service.SendQueuedAsync(user.Email, clock.GetUtcNow().UtcDateTime));

        Assert.Equal(2, sender.SentEmails.Count);
    }

    [Fact]
    public async Task Resend_should_not_expose_delivery_failure()
    {
        var users = new InMemoryUserRepository();
        var sender = new RecordingEmailVerificationEmailSender
        {
            FailDelivery = true
        };
        var service = new EmailVerificationService(
            users,
            new InMemoryEmailVerificationTokenRepository(),
            new EmailVerificationTokenGenerator(),
            sender,
            new EmailValidator(),
            new InMemoryEmailVerificationQueue(),
            new ManualTimeProvider(DateTimeOffset.UtcNow));
        User user = CreateUser();
        await users.AddAsync(user);

        await service.ResendAsync(user.Email);
    }

    [Fact]
    public async Task Resend_for_unknown_email_should_not_send_email()
    {
        var emailSender =
            new RecordingEmailVerificationEmailSender();
        var service = new EmailVerificationService(
            new InMemoryUserRepository(),
            new InMemoryEmailVerificationTokenRepository(),
            new EmailVerificationTokenGenerator(),
            emailSender,
            new EmailValidator(),
            new InMemoryEmailVerificationQueue(),
            new ManualTimeProvider(DateTimeOffset.UtcNow));

        await service.ResendAsync("missing@example.com");

        Assert.Empty(emailSender.SentEmails);
    }

    [Fact]
    public async Task Resend_for_verified_user_should_not_send_email()
    {
        var now = new DateTimeOffset(
            2026,
            9,
            4,
            12,
            0,
            0,
            TimeSpan.Zero);
        var userRepository = new InMemoryUserRepository();
        var tokenRepository =
            new InMemoryEmailVerificationTokenRepository();
        var emailSender =
            new RecordingEmailVerificationEmailSender();
        var service = new EmailVerificationService(
            userRepository,
            tokenRepository,
            new EmailVerificationTokenGenerator(),
            emailSender,
            new EmailValidator(),
            new InMemoryEmailVerificationQueue(),
            new ManualTimeProvider(now));
        User user = CreateUser();
        user.MarkEmailVerified(now.UtcDateTime);
        await userRepository.AddAsync(user);

        await service.ResendAsync(user.Email);

        Assert.Null(
            await tokenRepository.GetByUserIdAsync(user.Id));
        Assert.Empty(emailSender.SentEmails);
    }

    [Fact]
    public async Task Confirm_with_valid_token_should_verify_user_and_consume_token()
    {
        var now = new DateTimeOffset(
            2026,
            9,
            4,
            12,
            0,
            0,
            TimeSpan.Zero);
        var userRepository = new InMemoryUserRepository();
        var tokenRepository =
            new InMemoryEmailVerificationTokenRepository();
        var emailSender =
            new RecordingEmailVerificationEmailSender();
        var timeProvider = new ManualTimeProvider(now);
        var service = new EmailVerificationService(
            userRepository,
            tokenRepository,
            new EmailVerificationTokenGenerator(),
            emailSender,
            new EmailValidator(),
            new InMemoryEmailVerificationQueue(),
            timeProvider);
        User user = CreateUser();
        await userRepository.AddAsync(user);
        await service.IssueAsync(user);
        string rawToken =
            Assert.Single(emailSender.SentEmails).RawToken;

        timeProvider.Advance(TimeSpan.FromMinutes(5));
        await service.ConfirmAsync(rawToken);

        Assert.True(user.IsEmailVerified);
        Assert.Equal(
            now.AddMinutes(5).UtcDateTime,
            user.EmailVerifiedAtUtc);
        Assert.Null(
            await tokenRepository.GetByUserIdAsync(user.Id));
    }

    [Fact]
    public async Task Confirm_at_expiration_time_should_reject_token()
    {
        var now = new DateTimeOffset(
            2026,
            9,
            4,
            12,
            0,
            0,
            TimeSpan.Zero);
        var userRepository = new InMemoryUserRepository();
        var tokenRepository =
            new InMemoryEmailVerificationTokenRepository();
        var emailSender =
            new RecordingEmailVerificationEmailSender();
        var timeProvider = new ManualTimeProvider(now);
        var service = new EmailVerificationService(
            userRepository,
            tokenRepository,
            new EmailVerificationTokenGenerator(),
            emailSender,
            new EmailValidator(),
            new InMemoryEmailVerificationQueue(),
            timeProvider);
        User user = CreateUser();
        await userRepository.AddAsync(user);
        await service.IssueAsync(user);
        string rawToken =
            Assert.Single(emailSender.SentEmails).RawToken;

        timeProvider.Advance(TimeSpan.FromHours(8));
        ArgumentException exception =
            await Assert.ThrowsAsync<ArgumentException>(
                () => service.ConfirmAsync(rawToken));

        Assert.Equal(
            "Email verification token is invalid or expired. " +
            "(Parameter 'rawToken')",
            exception.Message);
        Assert.False(user.IsEmailVerified);
    }

    [Fact]
    public async Task Confirm_with_unknown_token_should_return_generic_error()
    {
        var service = new EmailVerificationService(
            new InMemoryUserRepository(),
            new InMemoryEmailVerificationTokenRepository(),
            new EmailVerificationTokenGenerator(),
            new RecordingEmailVerificationEmailSender(),
            new EmailValidator(),
            new InMemoryEmailVerificationQueue(),
            new ManualTimeProvider(DateTimeOffset.UtcNow));

        ArgumentException exception =
            await Assert.ThrowsAsync<ArgumentException>(
                () => service.ConfirmAsync(
                    "not-a-real-verification-token"));

        Assert.Equal(
            "Email verification token is invalid or expired. " +
            "(Parameter 'rawToken')",
            exception.Message);
    }

    [Fact]
    public async Task Confirm_with_consumed_token_should_reject_replay()
    {
        var now = new DateTimeOffset(
            2026,
            9,
            4,
            12,
            0,
            0,
            TimeSpan.Zero);
        var userRepository = new InMemoryUserRepository();
        var tokenRepository =
            new InMemoryEmailVerificationTokenRepository();
        var emailSender =
            new RecordingEmailVerificationEmailSender();
        var service = new EmailVerificationService(
            userRepository,
            tokenRepository,
            new EmailVerificationTokenGenerator(),
            emailSender,
            new EmailValidator(),
            new InMemoryEmailVerificationQueue(),
            new ManualTimeProvider(now));
        User user = CreateUser();
        await userRepository.AddAsync(user);
        await service.IssueAsync(user);
        string rawToken =
            Assert.Single(emailSender.SentEmails).RawToken;
        await service.ConfirmAsync(rawToken);

        ArgumentException exception =
            await Assert.ThrowsAsync<ArgumentException>(
                () => service.ConfirmAsync(rawToken));

        Assert.Equal(
            "Email verification token is invalid or expired. " +
            "(Parameter 'rawToken')",
            exception.Message);
    }

    private static User CreateUser()
    {
        return new User
        {
            FirstName = "Email",
            LastName = "Verification",
            Email = "customer@example.com",
            PasswordHash = "not-a-real-password-hash",
            CreatedAtUtc = new DateTime(
                2026, 9, 4, 11, 0, 0, DateTimeKind.Utc)
        };
    }

    private sealed class InMemoryEmailVerificationTokenRepository :
        IEmailVerificationTokenRepository
    {
        private readonly List<EmailVerificationToken> _tokens = [];
        private readonly SemaphoreSlim _replacementLock = new(1, 1);
        private TaskCompletionSource? _replacementGate;
        private int _coordinatedReplacements;

        public void CoordinateNextTwoReplacements()
        {
            _coordinatedReplacements = 0;
            _replacementGate = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public Task<EmailVerificationToken?> GetByUserIdAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                _tokens.SingleOrDefault(item => item.UserId == userId));
        }

        public Task<EmailVerificationToken?> GetByHashAsync(
            string tokenHash,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                _tokens.SingleOrDefault(
                    token => token.TokenHash == tokenHash));
        }

        public async Task<EmailVerificationTokenReplacement> TryReplaceAsync(
            EmailVerificationToken token,
            TimeSpan cooldown,
            CancellationToken cancellationToken = default)
        {
            TaskCompletionSource? gate = _replacementGate;
            if (gate is not null)
            {
                if (Interlocked.Increment(ref _coordinatedReplacements) == 2)
                {
                    gate.TrySetResult();
                }

                await gate.Task.WaitAsync(cancellationToken);
            }

            await _replacementLock.WaitAsync(cancellationToken);
            try
            {
                EmailVerificationToken? previous =
                    _tokens.SingleOrDefault(
                        existing => existing.UserId == token.UserId);
                if (previous is not null &&
                    token.CreatedAtUtc < previous.CreatedAtUtc.Add(cooldown))
                {
                    return new EmailVerificationTokenReplacement(
                        false,
                        previous);
                }

                _tokens.RemoveAll(
                    existing => existing.UserId == token.UserId);
                _tokens.Add(token);
                return new EmailVerificationTokenReplacement(true, previous);
            }
            finally
            {
                _replacementLock.Release();
            }
        }

        public async Task RestoreAsync(
            Guid replacementTokenId,
            EmailVerificationToken? previousToken,
            CancellationToken cancellationToken = default)
        {
            await _replacementLock.WaitAsync(cancellationToken);
            try
            {
                EmailVerificationToken? active = _tokens.SingleOrDefault(
                    token => token.Id == replacementTokenId);
                if (active is null)
                {
                    return;
                }

                _tokens.Remove(active);
                if (previousToken is not null &&
                    previousToken.UserId == active.UserId)
                {
                    _tokens.Add(previousToken);
                }
            }
            finally
            {
                _replacementLock.Release();
            }
        }

        public async Task<bool> TryConfirmAsync(
            string tokenHash,
            User user,
            DateTime verifiedAtUtc,
            CancellationToken cancellationToken = default)
        {
            await _replacementLock.WaitAsync(cancellationToken);
            try
            {
                EmailVerificationToken? token = _tokens.SingleOrDefault(
                    existing => existing.TokenHash == tokenHash);
                if (token is null || token.UserId != user.Id ||
                    token.IsExpiredAt(verifiedAtUtc))
                {
                    return false;
                }

                user.MarkEmailVerified(verifiedAtUtc);
                _tokens.Remove(token);
                return true;
            }
            finally
            {
                _replacementLock.Release();
            }
        }
    }

    private sealed class InMemoryUserRepository : IUserRepository
    {
        private readonly List<User> _users = [];

        // Implements the current user-store contract for these verification-only tests.
        public Task<int?> GetAuthVersionAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_users.SingleOrDefault(user => user.Id == userId)?.AuthVersion);
        }

        public Task<bool> RevokeSessionsAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            User? user = _users.SingleOrDefault(candidate => candidate.Id == userId);
            user?.RevokeSessions();
            return Task.FromResult(user is not null);
        }

        public Task<bool> ExistsByEmailAsync(
            string email,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                _users.Any(user => user.Email == email));
        }

        public Task<User?> GetByEmailAsync(
            string email,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                _users.SingleOrDefault(
                    user => user.Email == email));
        }

        public Task<User?> GetByIdAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                _users.SingleOrDefault(
                    user => user.Id == userId));
        }

        public Task<User?> GetByIdForUpdateAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                _users.SingleOrDefault(
                    user => user.Id == userId));
        }

        public Task AddAsync(
            User user,
            CancellationToken cancellationToken = default)
        {
            _users.Add(user);

            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryEmailVerificationQueue :
        IEmailVerificationQueue
    {
        private readonly Queue<EmailVerificationWorkItem> _jobs = new();

        public Task EnqueueAsync(
            string email,
            DateTime requestedAtUtc,
            CancellationToken cancellationToken = default)
        {
            _jobs.Enqueue(new EmailVerificationWorkItem(
                Guid.NewGuid(), email, requestedAtUtc, Guid.Empty, 0));
            return Task.CompletedTask;
        }

        public Task<EmailVerificationWorkItem?> ClaimAsync(
            DateTime nowUtc,
            CancellationToken cancellationToken = default)
        {
            EmailVerificationWorkItem? job =
                _jobs.Count == 0 ? null : _jobs.Dequeue();
            return Task.FromResult(job);
        }

        public Task CompleteAsync(
            EmailVerificationWorkItem job,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task RetryAsync(
            EmailVerificationWorkItem job,
            DateTime nowUtc,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingEmailVerificationEmailSender :
        IEmailVerificationEmailSender
    {
        public List<EmailVerificationEmail> SentEmails { get; } = [];

        public bool FailDelivery { get; set; }

        public Task SendAsync(
            EmailVerificationEmail email,
            CancellationToken cancellationToken = default)
        {
            if (FailDelivery)
            {
                throw new InvalidOperationException(
                    "Simulated email delivery failure.");
            }

            SentEmails.Add(email);

            return Task.CompletedTask;
        }
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;

        public ManualTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }

        public void Advance(TimeSpan duration)
        {
            _utcNow = _utcNow.Add(duration);
        }
    }
}
