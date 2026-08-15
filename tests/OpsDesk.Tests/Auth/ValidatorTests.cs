using OpsDesk.Application.Auth.Services;

namespace OpsDesk.Tests.Auth;

public sealed class ValidatorTests
{
    private readonly PasswordValidator _passwordValidator =
        new();

    private readonly EmailValidator _emailValidator =
        new();

    [Theory]
    [InlineData("abcdefg!")]
    [InlineData("Abcdefgh")]
    [InlineData("Abcde fg!")]
    [InlineData("Abcdeğf!")]
    public void Invalid_password_should_be_rejected(
        string password)
    {
        Assert.Throws<ArgumentException>(
            () => _passwordValidator.Validate(password));
    }

    [Theory]
    [InlineData("ABCDEFG!")]
    [InlineData("ValidPass!")]
    public void Valid_password_should_be_accepted(
        string password)
    {
        _passwordValidator.Validate(password);
    }

    [Theory]
    [InlineData("")]
    [InlineData("hamza")]
    [InlineData("hamza @example.com")]
    public void Invalid_email_should_be_rejected(
        string email)
    {
        Assert.Throws<ArgumentException>(
            () => _emailValidator.Validate(email));
    }

    [Fact]
    public void Valid_email_should_be_accepted()
    {
        _emailValidator.Validate("hamza@example.com");
    }
}