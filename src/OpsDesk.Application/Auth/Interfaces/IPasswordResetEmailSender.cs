using OpsDesk.Application.Auth.Models;

namespace OpsDesk.Application.Auth.Interfaces;

public interface IPasswordResetEmailSender
{
    // Delivers a recovery token without including the user's password.
    Task SendAsync(PasswordResetEmail email, CancellationToken cancellationToken = default);
}
