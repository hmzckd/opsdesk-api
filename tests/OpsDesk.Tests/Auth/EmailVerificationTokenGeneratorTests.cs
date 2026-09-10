using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Application.Auth.Models;
using OpsDesk.Infrastructure.Authentication;

namespace OpsDesk.Tests.Auth;

public sealed class EmailVerificationTokenGeneratorTests
{
    private readonly IEmailVerificationTokenGenerator _generator =
        new EmailVerificationTokenGenerator();

    [Fact]
    public void Compute_hash_should_match_sha256_known_value()
    {
        string result = _generator.ComputeHash("abc");

        Assert.Equal(
            "BA7816BF8F01CFEA414140DE5DAE2223" +
            "B00361A396177A9CB410FF61F20015AD",
            result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Compute_hash_should_reject_missing_raw_token(
        string rawToken)
    {
        Assert.Throws<ArgumentException>(
            () => _generator.ComputeHash(rawToken));
    }

    [Fact]
    public void Generate_token_should_return_url_safe_token_and_hash()
    {
        GeneratedEmailVerificationToken result =
            _generator.GenerateToken();

        Assert.Matches(
            "^[A-Za-z0-9_-]{43}$",
            result.RawToken);
        Assert.Matches(
            "^[A-F0-9]{64}$",
            result.TokenHash);
        Assert.Equal(
            _generator.ComputeHash(result.RawToken),
            result.TokenHash);
        Assert.NotEqual(
            result.RawToken,
            result.TokenHash);
    }

    [Fact]
    public void Generate_token_should_produce_fresh_raw_tokens()
    {
        GeneratedEmailVerificationToken first =
            _generator.GenerateToken();
        GeneratedEmailVerificationToken second =
            _generator.GenerateToken();

        Assert.NotEqual(first.RawToken, second.RawToken);
    }
}
