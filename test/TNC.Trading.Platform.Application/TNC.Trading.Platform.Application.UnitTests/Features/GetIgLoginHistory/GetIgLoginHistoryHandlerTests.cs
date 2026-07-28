using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.GetIgLoginHistory;
using TNC.Trading.Platform.Application.Features.GetIgLoginHistory.Ports;
using TNC.Trading.Platform.Application.Services;

namespace TNC.Trading.Platform.Application.UnitTests.Features.GetIgLoginHistory;

public sealed class GetIgLoginHistoryHandlerTests
{
    /// <summary>
    /// Verifies the history query passes the active broker environment to its read-only port and preserves adapter ordering.
    /// Expected: the handler returns the reader's sequence unchanged and performs no write operation.
    /// Why: the query must remain a narrow read slice after the login-history boundary extraction.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ShouldReturnRetainedSnapshotsInReaderOrder_WhenHistoryExists()
    {
        var configuration = CreateConfiguration(BrokerEnvironmentKind.Demo);
        var expectedSnapshots = new[] { CreateSnapshot("newer"), CreateSnapshot("older") };
        var reader = new FakeHistoryReader(expectedSnapshots);
        var handler = new GetIgLoginHistoryHandler(
            new PlatformConfigurationService(new FakeConfigurationStore(configuration)),
            reader);

        var response = await handler.HandleAsync(new GetIgLoginHistoryRequest(), CancellationToken.None);

        Assert.Equal(expectedSnapshots, response.RetainedSnapshots);
        Assert.Equal(BrokerEnvironmentKind.Demo, reader.Environment);
        Assert.Equal(1, reader.ReadCount);
    }

    /// <summary>
    /// Verifies an empty retained-history result crosses the query boundary without fabrication or failure.
    /// Expected: the response contains an empty sequence and the reader is called once.
    /// Why: a newly provisioned or not-yet-authenticated platform has valid empty history.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ShouldReturnEmptyHistory_WhenReaderHasNoSnapshots()
    {
        var reader = new FakeHistoryReader([]);
        var handler = CreateHandler(reader);

        var response = await handler.HandleAsync(new GetIgLoginHistoryRequest(), CancellationToken.None);

        Assert.Empty(response.RetainedSnapshots);
        Assert.Equal(1, reader.ReadCount);
    }

    /// <summary>
    /// Verifies cancellation crosses both configuration and history-read boundaries without being translated.
    /// Expected: the caller's cancellation exception propagates and the read receives the same token.
    /// Why: history requests must stop promptly without falling back to a write or reconciliation workflow.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ShouldPropagateCancellation_WhenHistoryReadIsCancelled()
    {
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();
        var reader = new FakeHistoryReader([], cancellationSource.Token);
        var handler = CreateHandler(reader);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            handler.HandleAsync(new GetIgLoginHistoryRequest(), cancellationSource.Token));

        Assert.Equal(cancellationSource.Token, reader.CancellationToken);
    }

    private static GetIgLoginHistoryHandler CreateHandler(FakeHistoryReader reader) =>
        new(
            new PlatformConfigurationService(new FakeConfigurationStore(CreateConfiguration(BrokerEnvironmentKind.Demo))),
            reader);

    private static PlatformConfigurationSnapshot CreateConfiguration(BrokerEnvironmentKind brokerEnvironment) =>
        new(
            PlatformEnvironmentKind.Live,
            brokerEnvironment,
            new TradingScheduleConfiguration(
                new TimeOnly(8, 0),
                new TimeOnly(16, 30),
                [DayOfWeek.Monday],
                WeekendBehavior.ExcludeWeekends,
                [],
                "UTC"),
            new RetryPolicyConfiguration(1, 1, 2, 60, 5),
            new NotificationSettingsConfiguration("RecordedOnly", "operator@example.com"),
            new CredentialPresence(false, false, false),
            true,
            true,
            DateTimeOffset.UtcNow,
            false);

    private static IgLoginSnapshot CreateSnapshot(string accountId) =>
        new(
            Guid.NewGuid(),
            BrokerEnvironmentKind.Demo,
            DateTimeOffset.UtcNow,
            DateOnly.FromDateTime(DateTime.UtcNow),
            IgLoginSnapshotKind.RetainedDailyFirstSuccessful,
            accountId,
            "https://stream.example.test",
            null,
            new Dictionary<string, string>(),
            "{}");

    private sealed class FakeHistoryReader(
        IReadOnlyList<IgLoginSnapshot> snapshots,
        CancellationToken cancellationTokenToThrow = default) : IGetIgLoginHistoryReader
    {
        public BrokerEnvironmentKind Environment { get; private set; }

        public int ReadCount { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public Task<IReadOnlyList<IgLoginSnapshot>> ReadAsync(
            BrokerEnvironmentKind brokerEnvironment,
            CancellationToken cancellationToken)
        {
            ReadCount++;
            Environment = brokerEnvironment;
            CancellationToken = cancellationToken;
            return cancellationTokenToThrow.CanBeCanceled
                ? Task.FromCanceled<IReadOnlyList<IgLoginSnapshot>>(cancellationTokenToThrow)
                : Task.FromResult(snapshots);
        }
    }

    private sealed class FakeConfigurationStore(PlatformConfigurationSnapshot configuration) : IPlatformConfigurationStore
    {
        public Task<PlatformConfigurationSnapshot> ApplyStartupConfigurationAsync(CancellationToken cancellationToken) => Task.FromResult(configuration);

        public Task<PlatformConfigurationSnapshot> GetCurrentAsync(CancellationToken cancellationToken) => Task.FromResult(configuration);

        public Task<PlatformConfigurationSnapshot> GetRuntimeAsync(
            PlatformEnvironmentKind? platformEnvironment,
            BrokerEnvironmentKind? brokerEnvironment,
            CancellationToken cancellationToken) => Task.FromResult(configuration);
    }
}