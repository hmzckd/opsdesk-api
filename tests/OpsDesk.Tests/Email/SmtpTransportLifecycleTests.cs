using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Options;
using OpsDesk.Application.Auth.Models;
using OpsDesk.Infrastructure.Email;

namespace OpsDesk.Tests.Email;

public sealed class SmtpTransportLifecycleTests
{
    // A server may accept DATA yet stop responding to QUIT; delivery must still complete successfully.
    [Fact]
    public async Task Accepted_email_should_not_wait_for_a_quit_reply()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var shutdown = new CancellationTokenSource();
        Task server = RunServerAsync(listener, shutdown.Token);
        var settings = new EmailSettings
        {
            Host = "127.0.0.1", Port = ((IPEndPoint)listener.LocalEndpoint).Port,
            FromAddress = "noreply@opsdesk.local", FromName = "OpsDesk"
        };
        var sender = new SmtpPasswordResetEmailSender(Options.Create(settings));
        try
        {
            await sender.SendAsync(new PasswordResetEmail("recipient@example.com", "pwd_test", DateTime.UtcNow.AddMinutes(30)))
                .WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            await shutdown.CancelAsync();
            listener.Stop();
            await server;
        }
    }

    // Implements only the SMTP commands needed by this test and deliberately never answers QUIT.
    private static async Task RunServerAsync(TcpListener listener, CancellationToken cancellationToken)
    {
        try
        {
            using TcpClient socket = await listener.AcceptTcpClientAsync(cancellationToken);
            using NetworkStream stream = socket.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
            using var writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true)
            {
                AutoFlush = true, NewLine = "\r\n"
            };
            await writer.WriteLineAsync("220 localhost test SMTP");
            bool readingData = false;
            while (await reader.ReadLineAsync(cancellationToken) is { } line)
            {
                if (readingData)
                {
                    if (line != ".") continue;
                    readingData = false;
                    await writer.WriteLineAsync("250 Message accepted");
                }
                else if (line.StartsWith("DATA", StringComparison.Ordinal))
                {
                    readingData = true;
                    await writer.WriteLineAsync("354 Send message");
                }
                else if (!line.StartsWith("QUIT", StringComparison.Ordinal))
                {
                    await writer.WriteLineAsync("250 OK");
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
    }
}
