using OpsDesk.Application.Auth.Services;

namespace OpsDesk.Tests.Auth;

public sealed class UserInputNormalizerTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_name_should_be_rejected(string name)
    {
        Assert.Throws<ArgumentException>(() =>
            UserInputNormalizer.NormalizeName(name, "FirstName"));
    }

    [Fact]
    public void Name_longer_than_database_limit_should_be_rejected()
    {
        string name = new(
            'A',
            UserInputNormalizer.MaximumNameLength + 1);

        Assert.Throws<ArgumentException>(() =>
            UserInputNormalizer.NormalizeName(name, "FirstName"));
    }

    [Fact]
    public void Valid_name_should_be_trimmed()
    {
        string normalizedName =
            UserInputNormalizer.NormalizeName(
                "  Hamza  ",
                "FirstName");

        Assert.Equal("Hamza", normalizedName);
    }

    [Fact]
    public void Email_should_be_trimmed_and_lowercased()
    {
        string normalizedEmail =
            UserInputNormalizer.NormalizeEmail(
                "  Hamza@Example.COM  ");

        Assert.Equal("hamza@example.com", normalizedEmail);
    }
}
