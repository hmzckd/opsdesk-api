namespace OpsDesk.Application.Auth.Services;

public static class UserInputNormalizer
{
    public const int MaximumNameLength = 100;

    public static string NormalizeName(
        string value,
        string fieldName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            value,
            fieldName);

        string normalizedValue = value.Trim();

        if (normalizedValue.Length > MaximumNameLength)
        {
            throw new ArgumentException(
                $"{fieldName} cannot contain more than " +
                $"{MaximumNameLength} characters.",
                fieldName);
        }

        return normalizedValue;
    }

    public static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }
}
