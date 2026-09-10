using System.Threading.Channels;
using System.Collections.Concurrent;
using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Application.Auth.Models;

namespace OpsDesk.Tests.Integration;

public sealed class PasswordResetMailbox : IPasswordResetEmailSender
{
    private readonly Channel<PasswordResetEmail> _messages = Channel.CreateUnbounded<PasswordResetEmail>();
    public ConcurrentQueue<PasswordResetEmail> Delivered { get; } = new();

    // Captures real worker delivery at the email adapter instead of using arbitrary test sleeps.
    public Task SendAsync(PasswordResetEmail email, CancellationToken cancellationToken = default)
    {
        Delivered.Enqueue(email);
        return _messages.Writer.WriteAsync(email, cancellationToken).AsTask();
    }

    // Waits for the matching delivery with a failure deadline, not a fixed success delay.
    public async Task<PasswordResetEmail> WaitForAsync(string recipient)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await foreach (PasswordResetEmail email in _messages.Reader.ReadAllAsync(deadline.Token))
        {
            if (email.RecipientEmail == recipient) return email;
        }
        throw new InvalidOperationException("Mailbox closed before delivery.");
    }
}
