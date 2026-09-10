using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Application.Auth.Models;
using OpsDesk.Domain.Entities;
using OpsDesk.Application.Auth.DTOs;

namespace OpsDesk.Application.Auth.Services;

public sealed class PasswordRecoveryService(
    IEmailValidator emailValidator, IPasswordRecoveryQueue queue, TimeProvider timeProvider,
    IUserRepository users, IPasswordResetTokenGenerator generator,
    IPasswordResetTokenRepository tokens, IPasswordResetEmailSender sender,
    IPasswordValidator passwordValidator, IPasswordHasher passwordHasher)
    : IPasswordRecoveryService
{
    // Checks the public input before asking persistence to perform the indivisible reset operation.
    public async Task ResetAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        string tokenHash = generator.ComputeHash(request.Token);
        PasswordResetToken? token = await tokens.GetByHashAsync(tokenHash, cancellationToken);
        if (token is null || !token.CanUseAt(timeProvider.GetUtcNow().UtcDateTime))
            throw new ArgumentException("Password reset token is invalid or expired.");
        passwordValidator.Validate(request.NewPassword);
        string passwordHash = passwordHasher.HashPassword(request.NewPassword);
        if (!await tokens.TryResetAsync(tokenHash, passwordHash, cancellationToken))
            throw new ArgumentException("Password reset token is invalid or expired.");
    }

    // Both known and unknown addresses follow the same durable enqueue path.
    public Task RequestAsync(string email, CancellationToken cancellationToken = default)
    {
        emailValidator.Validate(email);
        return queue.EnqueueAsync(UserInputNormalizer.NormalizeEmail(email),
            timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
    }

    // Runs outside HTTP: checks eligibility, issues a token atomically, then sends it without a DB lock.
    public async Task SendAsync(string email, DateTime requestedAtUtc,
        CancellationToken cancellationToken = default)
    {
        DateTime nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        if (requestedAtUtc > nowUtc || requestedAtUtc.AddMinutes(30) <= nowUtc) return;
        User? user = await users.GetByEmailAsync(email, cancellationToken);
        if (user is null || user.CreatedAtUtc > requestedAtUtc || string.IsNullOrEmpty(user.PasswordHash)) return;

        GeneratedPasswordResetToken generated = generator.GenerateToken();
        PasswordResetToken token = PasswordResetToken.Create(user.Id, generated.TokenHash, nowUtc);
        if (!await tokens.TryReplaceAsync(token, cancellationToken)) return;
        try
        {
            await sender.SendAsync(new PasswordResetEmail(email, generated.RawToken, token.ExpiresAtUtc),
                cancellationToken);
        }
        catch
        {
            // Cleanup is bounded and independent of an expired delivery cancellation token.
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await tokens.RevokeAsync(token.Id, timeProvider.GetUtcNow().UtcDateTime, cleanup.Token);
            throw;
        }
    }
}
