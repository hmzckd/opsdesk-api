using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OpsDesk.Api.BackgroundServices;
using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Application.Auth.Models;

namespace OpsDesk.Tests.Email;

public sealed class EmailVerificationWorkerTests
{
    [Fact]
    public async Task Worker_should_continue_after_transient_queue_failure()
    {
        var queue = new TransientFailureQueue();
        await using ServiceProvider services = new ServiceCollection()
            .AddSingleton<IEmailVerificationQueue>(queue)
            .BuildServiceProvider();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EmailVerification:WorkerEnabled"] = "true"
            })
            .Build();
        var worker = new EmailVerificationWorker(
            services.GetRequiredService<IServiceScopeFactory>(),
            configuration,
            TimeProvider.System,
            NullLogger<EmailVerificationWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);
        await queue.SecondClaim.Task.WaitAsync(TimeSpan.FromSeconds(8));
        await worker.StopAsync(CancellationToken.None);

        Assert.True(queue.ClaimAttempts >= 2);
    }

    private sealed class TransientFailureQueue : IEmailVerificationQueue
    {
        private int _claimAttempts;

        public int ClaimAttempts => Volatile.Read(ref _claimAttempts);

        public TaskCompletionSource SecondClaim { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public Task EnqueueAsync(
            string email,
            DateTime requestedAtUtc,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<EmailVerificationWorkItem?> ClaimAsync(
            DateTime nowUtc,
            CancellationToken cancellationToken = default)
        {
            int attempt = Interlocked.Increment(ref _claimAttempts);
            if (attempt == 1)
            {
                throw new InvalidOperationException(
                    "Simulated transient queue failure.");
            }

            SecondClaim.TrySetResult();
            return Task.FromResult<EmailVerificationWorkItem?>(null);
        }

        public Task CompleteAsync(
            EmailVerificationWorkItem workItem,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task RetryAsync(
            EmailVerificationWorkItem workItem,
            DateTime failedAtUtc,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
