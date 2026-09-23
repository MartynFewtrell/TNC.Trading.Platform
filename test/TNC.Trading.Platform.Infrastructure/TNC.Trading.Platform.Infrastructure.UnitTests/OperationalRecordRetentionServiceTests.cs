using Microsoft.Extensions.Logging;
using TNC.Trading.Platform.Infrastructure.Operations.Retention;

namespace TNC.Trading.Platform.Infrastructure.UnitTests;

public sealed class OperationalRecordRetentionServiceTests
{
    /// <summary>
    /// Trace: Operational record retention cancellation remediation Phase 2, Step 2.2.
    /// Verifies: cancellation while the worker is in its periodic wait completes the worker normally.
    /// Expected: the stopping-token cancellation is handled by the delay boundary without an error log.
    /// Why: normal host shutdown must not appear as an independent retention-processing failure.
    /// </summary>
    [Fact]
    public async Task RunUntilStoppedAsync_ShouldCompleteNormally_WhenStoppingTokenCancelsDuringDelay()
    {
        using var cancellationSource = new CancellationTokenSource();
        var logger = new RecordingLogger();
        var service = new OperationalRecordRetentionService(
            _ => Task.CompletedTask,
            logger,
            cancellationToken =>
            {
                cancellationSource.Cancel();
                return Task.FromCanceled(cancellationToken);
            });

        await service.RunUntilStoppedAsync(cancellationSource.Token);

        Assert.Empty(logger.LogLevels);
    }

    /// <summary>
    /// Trace: Operational record retention cancellation remediation Phase 2, Step 2.2.
    /// Verifies: cancellation raised by retention processing completes the worker normally.
    /// Expected: the worker returns at its filtered stopping-token cancellation boundary without an error log.
    /// Why: processor cooperation with normal host shutdown must not be converted into a retention failure.
    /// </summary>
    [Fact]
    public async Task RunUntilStoppedAsync_ShouldCompleteNormally_WhenStoppingTokenCancelsProcessor()
    {
        using var cancellationSource = new CancellationTokenSource();
        var logger = new RecordingLogger();
        var service = new OperationalRecordRetentionService(
            cancellationToken =>
            {
                cancellationSource.Cancel();
                return Task.FromCanceled(cancellationToken);
            },
            logger,
            _ => Task.CompletedTask);

        await service.RunUntilStoppedAsync(cancellationSource.Token);

        Assert.Empty(logger.LogLevels);
    }

    /// <summary>
    /// Trace: Operational record retention cancellation remediation Phase 2, Step 2.2.
    /// Verifies: a non-cancellation processing failure is logged before the worker follows its normal scheduling path.
    /// Expected: one error is logged and the deterministic delay then stops the loop.
    /// Why: lifecycle hardening must not hide genuine retention-processing failures.
    /// </summary>
    [Fact]
    public async Task RunUntilStoppedAsync_ShouldLogAndContinue_WhenProcessorThrowsNonCancellationException()
    {
        using var cancellationSource = new CancellationTokenSource();
        var logger = new RecordingLogger();
        var service = new OperationalRecordRetentionService(
            _ => Task.FromException(new InvalidOperationException("transient failure")),
            logger,
            _ =>
            {
                cancellationSource.Cancel();
                return Task.CompletedTask;
            });

        await service.RunUntilStoppedAsync(cancellationSource.Token);

        Assert.Equal([LogLevel.Error], logger.LogLevels);
    }

    private sealed class RecordingLogger : ILogger<OperationalRecordRetentionService>
    {
        public List<LogLevel> LogLevels { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            LogLevels.Add(logLevel);
        }
    }
}
