using OpsDesk.Domain.Entities;

namespace OpsDesk.Tests.Auth;

public sealed class UserEmailVerificationTests
{
    [Fact]
    public void New_user_should_start_with_unverified_email()
    {
        var user = new User();

        Assert.False(user.IsEmailVerified);
        Assert.Null(user.EmailVerifiedAtUtc);
    }

    [Fact]
    public void Mark_email_verified_should_store_utc_time()
    {
        var user = new User();
        var verifiedAtUtc = new DateTime(
            2026,
            9,
            4,
            12,
            0,
            0,
            DateTimeKind.Utc);

        bool changed = user.MarkEmailVerified(verifiedAtUtc);

        Assert.True(changed);
        Assert.True(user.IsEmailVerified);
        Assert.Equal(verifiedAtUtc, user.EmailVerifiedAtUtc);
    }

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void Mark_email_verified_should_reject_non_utc_time(
        DateTimeKind kind)
    {
        var user = new User();
        DateTime invalidTime = DateTime.SpecifyKind(
            new DateTime(2026, 9, 4, 12, 0, 0),
            kind);

        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => user.MarkEmailVerified(invalidTime));

        Assert.Equal("verifiedAtUtc", exception.ParamName);
        Assert.False(user.IsEmailVerified);
    }

    [Fact]
    public void Mark_email_verified_should_not_change_verified_user()
    {
        var user = new User();
        var firstVerifiedAtUtc = new DateTime(
            2026,
            9,
            4,
            12,
            0,
            0,
            DateTimeKind.Utc);
        DateTime secondVerifiedAtUtc =
            firstVerifiedAtUtc.AddMinutes(5);

        bool firstChange = user.MarkEmailVerified(firstVerifiedAtUtc);
        bool secondChange = user.MarkEmailVerified(secondVerifiedAtUtc);

        Assert.True(firstChange);
        Assert.False(secondChange);
        Assert.Equal(firstVerifiedAtUtc, user.EmailVerifiedAtUtc);
    }
}
