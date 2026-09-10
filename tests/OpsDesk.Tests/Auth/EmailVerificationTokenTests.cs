using OpsDesk.Domain.Entities;

namespace OpsDesk.Tests.Auth;

public sealed class EmailVerificationTokenTests
{
    [Fact]
    public void Create_should_record_token_details()
    {
        Guid userId = Guid.NewGuid();
        const string tokenHash = "stored-token-hash";
        var createdAtUtc = new DateTime(
            2026, 9, 4, 12, 0, 0, DateTimeKind.Utc);
        DateTime expiresAtUtc = createdAtUtc.AddHours(8);

        EmailVerificationToken token =
            EmailVerificationToken.Create(
                userId,
                tokenHash,
                createdAtUtc,
                expiresAtUtc);

        Assert.NotEqual(Guid.Empty, token.Id);
        Assert.Equal(userId, token.UserId);
        Assert.Equal(tokenHash, token.TokenHash);
        Assert.Equal(createdAtUtc, token.CreatedAtUtc);
        Assert.Equal(expiresAtUtc, token.ExpiresAtUtc);
    }

    [Fact]
    public void Create_should_reject_empty_user_id()
    {
        DateTime createdAtUtc = DateTime.UtcNow;

        ArgumentException exception =
            Assert.Throws<ArgumentException>(() =>
                EmailVerificationToken.Create(
                    Guid.Empty,
                    "stored-token-hash",
                    createdAtUtc,
                    createdAtUtc.AddHours(8)));

        Assert.Equal("userId", exception.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_should_reject_missing_token_hash(
        string tokenHash)
    {
        DateTime createdAtUtc = DateTime.UtcNow;

        ArgumentException exception =
            Assert.Throws<ArgumentException>(() =>
                EmailVerificationToken.Create(
                    Guid.NewGuid(),
                    tokenHash,
                    createdAtUtc,
                    createdAtUtc.AddHours(8)));

        Assert.Equal("tokenHash", exception.ParamName);
    }

    [Theory]
    [InlineData(DateTimeKind.Local, true)]
    [InlineData(DateTimeKind.Unspecified, true)]
    [InlineData(DateTimeKind.Local, false)]
    [InlineData(DateTimeKind.Unspecified, false)]
    public void Create_should_reject_non_utc_times(
        DateTimeKind kind,
        bool creationTimeIsInvalid)
    {
        DateTime utcTime = new(
            2026, 9, 4, 12, 0, 0, DateTimeKind.Utc);
        DateTime invalidTime = DateTime.SpecifyKind(utcTime, kind);
        DateTime createdAtUtc =
            creationTimeIsInvalid ? invalidTime : utcTime;
        DateTime expiresAtUtc =
            creationTimeIsInvalid
                ? utcTime.AddHours(8)
                : invalidTime.AddHours(8);

        ArgumentException exception =
            Assert.Throws<ArgumentException>(() =>
                EmailVerificationToken.Create(
                    Guid.NewGuid(),
                    "stored-token-hash",
                    createdAtUtc,
                    expiresAtUtc));

        string expectedParameter = creationTimeIsInvalid
            ? "createdAtUtc"
            : "expiresAtUtc";
        Assert.Equal(expectedParameter, exception.ParamName);
    }

    [Fact]
    public void Create_should_require_expiry_after_creation()
    {
        DateTime createdAtUtc = DateTime.UtcNow;

        ArgumentException exception =
            Assert.Throws<ArgumentException>(() =>
                EmailVerificationToken.Create(
                    Guid.NewGuid(),
                    "stored-token-hash",
                    createdAtUtc,
                    createdAtUtc));

        Assert.Equal("expiresAtUtc", exception.ParamName);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    public void Is_expired_at_should_use_expiration_boundary(
        int secondsAfterExpiration,
        bool expected)
    {
        var createdAtUtc = new DateTime(
            2026, 9, 4, 12, 0, 0, DateTimeKind.Utc);
        DateTime expiresAtUtc = createdAtUtc.AddHours(8);
        EmailVerificationToken token =
            EmailVerificationToken.Create(
                Guid.NewGuid(),
                "stored-token-hash",
                createdAtUtc,
                expiresAtUtc);

        bool result = token.IsExpiredAt(
            expiresAtUtc.AddSeconds(secondsAfterExpiration));

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Is_expired_at_should_reject_non_utc_time()
    {
        DateTime createdAtUtc = DateTime.UtcNow;
        EmailVerificationToken token =
            EmailVerificationToken.Create(
                Guid.NewGuid(),
                "stored-token-hash",
                createdAtUtc,
                createdAtUtc.AddHours(8));
        DateTime localTime = DateTime.SpecifyKind(
            createdAtUtc,
            DateTimeKind.Local);

        ArgumentException exception =
            Assert.Throws<ArgumentException>(
                () => token.IsExpiredAt(localTime));

        Assert.Equal("checkedAtUtc", exception.ParamName);
    }
}
