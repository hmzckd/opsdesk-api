using Microsoft.AspNetCore.Identity;
using OpsDesk.Application.Auth.Interfaces;

namespace OpsDesk.Infrastructure.Authentication;

public sealed class PasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<object> _passwordHasher = new();
    private readonly object _userContext = new();

    public string HashPassword(string password)
    {
        return _passwordHasher.HashPassword(
            _userContext,
            password);
    }

    public bool VerifyPassword(
        string password,
        string passwordHash)
    {
        PasswordVerificationResult result =
            _passwordHasher.VerifyHashedPassword(
                _userContext,
                passwordHash,
                password);

        return result is
            PasswordVerificationResult.Success or
            PasswordVerificationResult.SuccessRehashNeeded;
    }
}