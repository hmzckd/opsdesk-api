using OpsDesk.Application.Auth.Interfaces;
using System;
using System.Linq;

namespace OpsDesk.Application.Auth.Services;

public sealed class PasswordValidator : IPasswordValidator
{
    private const int MinimumLength = 8;

    private const string TurkishCharacters =
        "çÇğĞıİöÖşŞüÜ";

    private const string AllowedSymbols =
        "!@#$%^&*()-_=+[]{};:'\",.<>/?\\|`~";

    public void Validate(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        if (password.Length < MinimumLength)
        {
            throw new ArgumentException(
                $"Password must contain at least {MinimumLength} characters.",
                nameof(password));
        }

        if (password.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException(
                "Password cannot contain whitespace.",
                nameof(password));
        }

        if (password.Any(
                character =>
                    TurkishCharacters.Contains(character)))
        {
            throw new ArgumentException(
                "Password cannot contain Turkish characters.",
                nameof(password));
        }

        if (!password.Any(
                character =>
                    character is >= 'A' and <= 'Z'))
        {
            throw new ArgumentException(
                "Password must contain at least one uppercase letter.",
                nameof(password));
        }

        if (!password.Any(
                character =>
                    AllowedSymbols.Contains(character)))
        {
            throw new ArgumentException(
                "Password must contain at least one symbol.",
                nameof(password));
        }
    }
}