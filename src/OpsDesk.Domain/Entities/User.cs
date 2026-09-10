using OpsDesk.Domain.Enums;

namespace OpsDesk.Domain.Entities;

public sealed class User
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? PasswordHash { get; set; }

    public int AuthVersion { get; private set; }

    // Replaces the stored hash and advances the version carried by every authenticated session.
    public void ResetPassword(string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        if (passwordHash.Length > 500) throw new ArgumentException("Password hash is too long.", nameof(passwordHash));
        int nextVersion = checked(AuthVersion + 1);
        PasswordHash = passwordHash;
        AuthVersion = nextVersion;
    }

    // Invalidates existing authenticated sessions without changing profile or password data.
    public void RevokeSessions()
    {
        AuthVersion = checked(AuthVersion + 1);
    }

    public UserRole Role { get; set; } = UserRole.Customer;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? EmailVerifiedAtUtc { get; private set; }

    public bool IsEmailVerified =>
        EmailVerifiedAtUtc.HasValue;

    /// <summary>
    /// Marks the User's email as verified and reports a state change.
    /// </summary>
    public bool MarkEmailVerified(DateTime verifiedAtUtc)
    {
        if (verifiedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "Email verification time must be UTC.",
                nameof(verifiedAtUtc));
        }

        if (IsEmailVerified)
        {
            return false;
        }

        EmailVerifiedAtUtc = verifiedAtUtc;

        return true;
    }
}
