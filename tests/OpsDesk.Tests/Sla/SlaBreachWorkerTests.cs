using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OpsDesk.Api.BackgroundServices;
using OpsDesk.Api.Configuration;
using OpsDesk.Application.Sla.Interfaces;

namespace OpsDesk.Tests.Sla;

public sealed class SlaBreachWorkerTests
{
    /// <summary>
    /// Verifies the worker immediately runs one pass with the configured limit.
    /// </summary>
    [Fact]
    public async Task Worker_should_run_an_immediate_configured_batch()
    {
        var service = new RecordingSlaBreachService();
        await using ServiceProvider services = new ServiceCollection()
            .AddScoped<ISlaBreachService>(_ => service)
            .BuildServiceProvider();
        var settings = new SlaBreachSettings
        {
            WorkerEnabled = true,
            PollIntervalSeconds = 60,
            BatchSize = 100
        };
        var worker = new SlaBreachWorker(
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(settings),
            TimeProvider.System,
            NullLogger<SlaBreachWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);
        try
        {
            int receivedBatchSize = await service.BatchReceived.Task
                .WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(100, receivedBatchSize);
        }
        finally
        {
            await StopWorkerAsync(worker);
        }
    }

    /// <summary>
    /// Verifies explicitly disabled monitoring performs no detection pass.
    /// </summary>
    [Fact]
    public async Task Disabled_worker_should_not_run_a_batch()
    {
        var service = new RecordingSlaBreachService();
        await using ServiceProvider services = new ServiceCollection()
            .AddScoped<ISlaBreachService>(_ => service)
            .BuildServiceProvider();
        var settings = new SlaBreachSettings
        {
            WorkerEnabled = false,
            PollIntervalSeconds = 60,
            BatchSize = 100
        };
        var worker = new SlaBreachWorker(
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(settings),
            TimeProvider.System,
            NullLogger<SlaBreachWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);
        try
        {
            Assert.False(service.BatchReceived.Task.IsCompleted);
        }
        finally
        {
            await StopWorkerAsync(worker);
        }
    }

    /// <summary>
    /// Verifies one timer tick starts another detection pass after the immediate pass.
    /// </summary>
    [Fact]
    public async Task Worker_should_repeat_detection_after_poll_interval()
    {
        var service = new RepeatingSlaBreachService();
        await using ServiceProvider services = new ServiceCollection()
            .AddScoped<ISlaBreachService>(_ => service)
            .BuildServiceProvider();
        var timeProvider = new ManualTimerTimeProvider();
        var settings = new SlaBreachSettings
        {
            WorkerEnabled = true,
            PollIntervalSeconds = 60,
            BatchSize = 10
        };
        var worker = new SlaBreachWorker(
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(settings),
            timeProvider,
            NullLogger<SlaBreachWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);
        try
        {
            await service.FirstBatchCompleted.Task
                .WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(TimeSpan.FromSeconds(60), timeProvider.DueTime);
            Assert.Equal(TimeSpan.FromSeconds(60), timeProvider.Period);
            timeProvider.FireTimer();
            await service.SecondBatchCompleted.Task
                .WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(2, service.CallCount);
        }
        finally
        {
            await StopWorkerAsync(worker);
        }
    }

    /// <summary>
    /// Verifies each detection pass resolves a new scoped service instance.
    /// </summary>
    [Fact]
    public async Task Worker_should_create_a_fresh_scope_for_each_batch()
    {
        var probe = new ScopeProbe();
        await using ServiceProvider services = new ServiceCollection()
            .AddSingleton(probe)
            .AddScoped<ISlaBreachService, ScopedSlaBreachService>()
            .BuildServiceProvider();
        var timeProvider = new ManualTimerTimeProvider();
        var worker = CreateEnabledWorker(
            services,
            timeProvider,
            batchSize: 10);

        await worker.StartAsync(CancellationToken.None);
        try
        {
            await probe.FirstBatchCompleted.Task
                .WaitAsync(TimeSpan.FromSeconds(5));
            timeProvider.FireTimer();
            await probe.SecondBatchCompleted.Task
                .WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(2, probe.ServiceIds.Count);
            Assert.NotEqual(probe.ServiceIds[0], probe.ServiceIds[1]);
        }
        finally
        {
            await StopWorkerAsync(worker);
        }
    }

    /// <summary>
    /// Verifies a transient batch failure is retried on the next timer tick.
    /// </summary>
    [Fact]
    public async Task Worker_should_retry_after_a_batch_failure()
    {
        var service = new FailOnceSlaBreachService();
        await using ServiceProvider services = new ServiceCollection()
            .AddScoped<ISlaBreachService>(_ => service)
            .BuildServiceProvider();
        var timeProvider = new ManualTimerTimeProvider();
        var worker = CreateEnabledWorker(
            services,
            timeProvider,
            batchSize: 10);

        await worker.StartAsync(CancellationToken.None);
        try
        {
            await service.FirstAttemptCompleted.Task
                .WaitAsync(TimeSpan.FromSeconds(5));
            timeProvider.FireTimer();
            await service.SuccessfulRetryCompleted.Task
                .WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(2, service.CallCount);
        }
        finally
        {
            await StopWorkerAsync(worker);
        }
    }

    /// <summary>
    /// Verifies stopping the host cancels an in-flight detection pass.
    /// </summary>
    [Fact]
    public async Task Worker_should_cancel_an_in_flight_batch_when_stopped()
    {
        var service = new CancellableSlaBreachService();
        await using ServiceProvider services = new ServiceCollection()
            .AddScoped<ISlaBreachService>(_ => service)
            .BuildServiceProvider();
        var worker = CreateEnabledWorker(
            services,
            new ManualTimerTimeProvider(),
            batchSize: 10);

        await worker.StartAsync(CancellationToken.None);
        try
        {
            await service.BatchStarted.Task
                .WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            await StopWorkerAsync(worker);
        }

        await service.CancellationObserved.Task
            .WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(service.CancellationObserved.Task.IsCompleted);
    }

    /// <summary>
    /// Stops a worker even when an assertion or bounded wait fails.
    /// </summary>
    private static async Task StopWorkerAsync(SlaBreachWorker worker)
    {
        using var timeout = new CancellationTokenSource(
            TimeSpan.FromSeconds(5));
        await worker.StopAsync(timeout.Token);
    }

    /// <summary>
    /// Creates an enabled worker for tests that exercise repeated polling.
    /// </summary>
    private static SlaBreachWorker CreateEnabledWorker(
        ServiceProvider services,
        TimeProvider timeProvider,
        int batchSize)
    {
        var settings = new SlaBreachSettings
        {
            WorkerEnabled = true,
            PollIntervalSeconds = 60,
            BatchSize = batchSize
        };

        return new SlaBreachWorker(
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(settings),
            timeProvider,
            NullLogger<SlaBreachWorker>.Instance);
    }

    private sealed class RecordingSlaBreachService :
        ISlaBreachService
    {
        public TaskCompletionSource<int> BatchReceived { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<int> DetectAndRecordAsync(
            int maximumCandidates,
            CancellationToken cancellationToken = default)
        {
            BatchReceived.TrySetResult(maximumCandidates);
            return Task.FromResult(0);
        }
    }

    private sealed class RepeatingSlaBreachService : ISlaBreachService
    {
        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);

        public TaskCompletionSource FirstBatchCompleted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource SecondBatchCompleted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<int> DetectAndRecordAsync(
            int maximumCandidates,
            CancellationToken cancellationToken = default)
        {
            int callCount = Interlocked.Increment(ref _callCount);

            if (callCount == 1)
            {
                FirstBatchCompleted.TrySetResult();
            }
            else if (callCount == 2)
            {
                SecondBatchCompleted.TrySetResult();
            }

            return Task.FromResult(0);
        }
    }

    private sealed class ScopeProbe
    {
        private readonly object _syncRoot = new();

        public List<Guid> ServiceIds { get; } = [];

        public TaskCompletionSource FirstBatchCompleted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource SecondBatchCompleted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Record(Guid serviceId)
        {
            lock (_syncRoot)
            {
                ServiceIds.Add(serviceId);

                if (ServiceIds.Count == 1)
                {
                    FirstBatchCompleted.TrySetResult();
                }
                else if (ServiceIds.Count == 2)
                {
                    SecondBatchCompleted.TrySetResult();
                }
            }
        }
    }

    private sealed class ScopedSlaBreachService(ScopeProbe probe) :
        ISlaBreachService
    {
        private readonly Guid _serviceId = Guid.NewGuid();

        public Task<int> DetectAndRecordAsync(
            int maximumCandidates,
            CancellationToken cancellationToken = default)
        {
            probe.Record(_serviceId);
            return Task.FromResult(0);
        }
    }

    private sealed class FailOnceSlaBreachService : ISlaBreachService
    {
        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);

        public TaskCompletionSource FirstAttemptCompleted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource SuccessfulRetryCompleted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<int> DetectAndRecordAsync(
            int maximumCandidates,
            CancellationToken cancellationToken = default)
        {
            int callCount = Interlocked.Increment(ref _callCount);

            if (callCount == 1)
            {
                FirstAttemptCompleted.TrySetResult();
                throw new InvalidOperationException(
                    "Simulated transient database failure.");
            }

            SuccessfulRetryCompleted.TrySetResult();
            return Task.FromResult(0);
        }
    }

    private sealed class CancellableSlaBreachService : ISlaBreachService
    {
        private readonly TaskCompletionSource _neverCompletes =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource BatchStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource CancellationObserved { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<int> DetectAndRecordAsync(
            int maximumCandidates,
            CancellationToken cancellationToken = default)
        {
            BatchStarted.TrySetResult();

            try
            {
                await _neverCompletes.Task.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                CancellationObserved.TrySetResult();
                throw;
            }

            return 0;
        }
    }

    private sealed class ManualTimerTimeProvider : TimeProvider
    {
        private ManualTimer? _timer;

        public TimeSpan? DueTime { get; private set; }

        public TimeSpan? Period { get; private set; }

        /// <summary>
        /// Captures PeriodicTimer's callback so a test can advance one poll deterministically.
        /// </summary>
        public override ITimer CreateTimer(
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            var timer = new ManualTimer(callback, state);
            DueTime = dueTime;
            Period = period;
            _timer = timer;
            return timer;
        }

        /// <summary>
        /// Simulates one configured timer interval without waiting for wall-clock time.
        /// </summary>
        public void FireTimer()
        {
            ManualTimer timer = _timer ?? throw new InvalidOperationException(
                "The worker has not created its timer yet.");
            timer.Fire();
        }

        private sealed class ManualTimer(
            TimerCallback callback,
            object? state) : ITimer
        {
            private bool _disposed;

            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                return !_disposed;
            }

            public void Dispose()
            {
                _disposed = true;
            }

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }

            public void Fire()
            {
                if (!_disposed)
                {
                    callback(state);
                }
            }
        }
    }
}
