using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Application.Auth.Models;
using OpsDesk.Domain.Entities;

namespace OpsDesk.Application.Auth.Services;

public sealed class EmailVerificationService :
    IEmailVerificationService
{
    private static readonly TimeSpan TokenLifetime =
        TimeSpan.FromHours(8);
    private static readonly TimeSpan ResendCooldown =
        TimeSpan.FromSeconds(60);

    private readonly IUserRepository _userRepository;
    private readonly IEmailVerificationTokenRepository
        _tokenRepository;
    private readonly IEmailVerificationTokenGenerator
        _tokenGenerator;
    private readonly IEmailVerificationEmailSender
        _emailSender;
    private readonly IEmailValidator _emailValidator;
    private readonly IEmailVerificationQueue _queue;
    private readonly TimeProvider _timeProvider;

    public EmailVerificationService(
        IUserRepository userRepository,
        IEmailVerificationTokenRepository tokenRepository,
        IEmailVerificationTokenGenerator tokenGenerator,
        IEmailVerificationEmailSender emailSender,
        IEmailValidator emailValidator,
        IEmailVerificationQueue queue,
        TimeProvider timeProvider)
    {
        _userRepository = userRepository;
        _tokenRepository = tokenRepository;
        _tokenGenerator = tokenGenerator;
        _emailSender = emailSender;
        _emailValidator = emailValidator;
        _queue = queue;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Creates an eight-hour token, stores its hash, and sends its raw value.
    /// </summary>
    public async Task IssueAsync(
        User user,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (user.IsEmailVerified)
        {
            return;
        }

        DateTime createdAtUtc =
            _timeProvider.GetUtcNow().UtcDateTime;

        await CreateAndSendAsync(
            user,
            createdAtUtc,
            cancellationToken);
    }

    /// <summary>
    /// Resends only after the cooldown without disclosing account existence.
    /// </summary>
    public Task ResendAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        _emailValidator.Validate(email);

        string normalizedEmail =
            UserInputNormalizer.NormalizeEmail(email);

        return _queue.EnqueueAsync(
            normalizedEmail,
            _timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken);
    }

    /// <summary>
    /// Checks eligibility and sends queued work without exposing the result to HTTP.
    /// </summary>
    public async Task SendQueuedAsync(
        string email,
        DateTime requestedAtUtc,
        CancellationToken cancellationToken = default)
    {
        DateTime createdAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
        if (requestedAtUtc > createdAtUtc ||
            requestedAtUtc.AddHours(8) <= createdAtUtc)
        {
            return;
        }

        User? user = await _userRepository.GetByEmailAsync(
            email,
            cancellationToken);
        if (user is null || user.IsEmailVerified ||
            user.CreatedAtUtc > requestedAtUtc)
        {
            return;
        }

        await CreateAndSendAsync(user, createdAtUtc, cancellationToken);
    }

    /// <summary>
    /// Validates a raw token, verifies its User, and consumes the token.
    /// </summary>
    public async Task ConfirmAsync(
        string rawToken,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawToken);

        string tokenHash =
            _tokenGenerator.ComputeHash(rawToken);

        EmailVerificationToken? token =
            await _tokenRepository.GetByHashAsync(
                tokenHash,
                cancellationToken);

        DateTime verifiedAtUtc =
            _timeProvider.GetUtcNow().UtcDateTime;

        if (token is null || token.IsExpiredAt(verifiedAtUtc))
        {
            throw CreateInvalidTokenException();
        }

        User? user =
            await _userRepository.GetByIdAsync(
                token.UserId,
                cancellationToken);

        if (user is null)
        {
            throw CreateInvalidTokenException();
        }

        bool confirmed = await _tokenRepository.TryConfirmAsync(
            tokenHash,
            user,
            verifiedAtUtc,
            cancellationToken);
        if (!confirmed)
        {
            throw CreateInvalidTokenException();
        }
    }

    private async Task CreateAndSendAsync(
        User user,
        DateTime createdAtUtc,
        CancellationToken cancellationToken)
    {
        GeneratedEmailVerificationToken generatedToken =
            _tokenGenerator.GenerateToken();

        EmailVerificationToken token =
            EmailVerificationToken.Create(
                user.Id,
                generatedToken.TokenHash,
                createdAtUtc,
                createdAtUtc.Add(TokenLifetime));

        EmailVerificationTokenReplacement replacement =
            await _tokenRepository.TryReplaceAsync(
                token,
                ResendCooldown,
                cancellationToken);
        if (!replacement.Replaced)
        {
            return;
        }

        var email = new EmailVerificationEmail(
            user.Email,
            generatedToken.RawToken,
            token.ExpiresAtUtc);

        try
        {
            await _emailSender.SendAsync(
                email,
                cancellationToken);
        }
        catch
        {
            using var cleanup =
                new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await _tokenRepository.RestoreAsync(
                token.Id,
                replacement.PreviousToken,
                cleanup.Token);
            throw;
        }
    }

    private static ArgumentException CreateInvalidTokenException()
    {
        return new ArgumentException(
            "Email verification token is invalid or expired.",
            "rawToken");
    }
}
