using Microsoft.Extensions.Logging.Abstractions;
using TNC.Trading.Platform.Application.Services;

namespace TNC.Trading.Platform.Application.UnitTests;

public class PlatformAuthSupervisorTests
{
    [Fact]
    public async Task RunUntilStoppedAsync_ShouldUseInjectedDelayWithoutRealTimeWait()
    {
        var tickRunner = new FakeTickRunner();
        using var cancellationSource = new CancellationTokenSource();
        var delay = new CancellingDelay(cancellationSource);
        var supervisor = new PlatformAuthSupervisor(tickRunner, delay);

        await supervisor.RunUntilStoppedAsync(cancellationSource.Token);

        Assert.Equal(1, tickRunner.CallCount);
        Assert.Equal(1, delay.CallCount);
    }

    [Fact]
    public async Task RunUntilStoppedAsync_ShouldStopCleanly_WhenRunnerCancelsWithStoppingToken()
    {
        var tickRunner = new CancellingTickRunner();
        var delay = new NeverReachedDelay();
        var supervisor = new PlatformAuthSupervisor(tickRunner, delay);
        using var cancellationSource = new CancellationTokenSource();

        cancellationSource.Cancel();
        await supervisor.RunUntilStoppedAsync(cancellationSource.Token);

        Assert.Equal(0, tickRunner.CallCount);
        Assert.Equal(0, delay.CallCount);
    }

    private sealed class FakeTickRunner : IPlatformAuthSupervisorTickRunner
    {
        public int CallCount { get; private set; }

        public Task RunSingleTickAsync(CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class CancellingTickRunner : IPlatformAuthSupervisorTickRunner
    {
        public int CallCount { get; private set; }

        public Task RunSingleTickAsync(CancellationToken cancellationToken)
        {
            CallCount++;
            throw new OperationCanceledException(cancellationToken);
        }
    }

    private sealed class CancellingDelay(CancellationTokenSource cancellationSource) : IPlatformAuthSupervisorDelay
    {
        public int CallCount { get; private set; }

        public Task DelayAsync(CancellationToken cancellationToken)
        {
            CallCount++;
            cancellationSource.Cancel();
            return Task.FromCanceled(cancellationToken);
        }
    }

    private sealed class NeverReachedDelay : IPlatformAuthSupervisorDelay
    {
        public int CallCount { get; private set; }

        public Task DelayAsync(CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.CompletedTask;
        }
    }
}