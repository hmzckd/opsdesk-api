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

    /// <summary>
    /// Validates the length, whitespace, structure, and domain of an email.
    /// </summary>
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

        if (!HasQualifiedDomain(trimmedEmail))
        {
            throw new ArgumentException(
                "Email must use a qualified domain, " +
                "for example user@company.com.",
                nameof(email));
        }
    }

    /// <summary>
    /// Checks that the domain has at least two non-empty labels.
    /// </summary>
    private static bool HasQualifiedDomain(string email)
    {
        int separatorIndex = email.LastIndexOf('@');

        if (separatorIndex < 0 || separatorIndex == email.Length - 1)
        {
            return false;
        }

        string[] domainLabels = email[(separatorIndex + 1)..]
            .Split('.', StringSplitOptions.None);

        return domainLabels.Length >= 2 &&
            domainLabels.All(label => label.Length > 0);
    }
}
