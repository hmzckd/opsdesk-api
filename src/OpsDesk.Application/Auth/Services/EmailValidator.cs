using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using OpsDesk.Application.Auth.Interfaces;

namespace OpsDesk.Application.Auth.Services;

public sealed class EmailValidator : IEmailValidator
{
    private const int MaximumLength = 320;

    private static readonly EmailAddressAttribute EmailAddressValidator =
        new();

    public void Validate(string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        string trimmedEmail = email.Trim();

        if (trimmedEmail.Length > MaximumLength)
        {
            throw new ArgumentException(
                $"Email cannot contain more than {MaximumLength} characters.",
                nameof(email));
        }

        if (trimmedEmail.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException(
                "Email cannot contain whitespace.",
                nameof(email));
        }

        if (!EmailAddressValidator.IsValid(trimmedEmail))
        {
            throw new ArgumentException(
                "Email address format is invalid.",
                nameof(email));
        }
    }
}