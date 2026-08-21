namespace OpsDesk.Application.Common.Exceptions;

/// <summary>
/// Represents an authenticated User attempting a forbidden operation.
/// </summary>
public sealed class ForbiddenException : Exception
{
    public ForbiddenException(string message)
        : base(message)
    {
    }
}
