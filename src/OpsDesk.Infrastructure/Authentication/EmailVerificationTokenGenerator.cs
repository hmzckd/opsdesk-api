using System.Security.Cryptography;
using System.Text;
using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Application.Auth.Models;

namespace OpsDesk.Infrastructure.Authentication;

public sealed class EmailVerificationTokenGenerator :
    IEmailVerificationTokenGenerator
{
    private const int TokenSizeInBytes = 32;

    /// <summary>
    /// Generates 256 random bits and returns a URL-safe token with its hash.
    /// </summary>
    public GeneratedEmailVerificationToken GenerateToken()
    {
        byte[] tokenBytes =
            RandomNumberGenerator.GetBytes(TokenSizeInBytes);

        string rawToken = Convert.ToBase64String(tokenBytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        return new GeneratedEmailVerificationToken(
            rawToken,
            ComputeHash(rawToken));
    }

    /// <summary>
    /// Computes a deterministic SHA-256 hash without retaining the raw token.
    /// </summary>
    public string ComputeHash(string rawToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawToken);

        byte[] rawTokenBytes =
            Encoding.UTF8.GetBytes(rawToken);

        byte[] hashBytes =
            SHA256.HashData(rawTokenBytes);

        return Convert.ToHexString(hashBytes);
    }
}
