using System;
using System.Collections.Generic;
using System.Globalization;
using OpsDesk.Application.Authorization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Application.Auth.Models;
using OpsDesk.Domain.Entities;

namespace OpsDesk.Infrastructure.Authentication;

public sealed class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly JwtSettings _settings;

    public JwtTokenGenerator(IOptions<JwtSettings> options)
    {
        _settings = options.Value;

        ValidateSettings(_settings);
    }

    public JwtTokenResult GenerateToken(User user)
    {
        DateTime issuedAtUtc = DateTime.UtcNow;
        DateTime expiresAtUtc = issuedAtUtc.AddMinutes(
            _settings.ExpirationMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(
                JwtRegisteredClaimNames.Iat,
                new DateTimeOffset(issuedAtUtc)
                    .ToUnixTimeSeconds()
                    .ToString(),
                ClaimValueTypes.Integer64),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(AuthClaimTypes.AuthVersion, user.AuthVersion.ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer32),
            new(
                ClaimTypes.Name,
                $"{user.FirstName} {user.LastName}".Trim()),
            new(ClaimTypes.Role, user.Role.ToString())
        };

        var securityKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_settings.SecretKey));

        var signingCredentials = new SigningCredentials(
            securityKey,
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: issuedAtUtc,
            expires: expiresAtUtc,
            signingCredentials: signingCredentials);

        string accessToken =
            new JwtSecurityTokenHandler().WriteToken(token);

        return new JwtTokenResult(
            accessToken,
            expiresAtUtc);
    }

    private static void ValidateSettings(JwtSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Issuer))
        {
            throw new InvalidOperationException(
                "JWT issuer is not configured.");
        }

        if (string.IsNullOrWhiteSpace(settings.Audience))
        {
            throw new InvalidOperationException(
                "JWT audience is not configured.");
        }

        if (Encoding.UTF8.GetByteCount(settings.SecretKey) < 32)
        {
            throw new InvalidOperationException(
                "JWT secret key must be at least 32 bytes.");
        }

        if (settings.ExpirationMinutes <= 0)
        {
            throw new InvalidOperationException(
                "JWT expiration must be greater than zero.");
        }
    }
}
