using TNC.Trading.Platform.Application.Authentication;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.RecordAuthAuditEvent;
using TNC.Trading.Platform.Application.Features.RecordAuthAuditEvent.Ports;

namespace TNC.Trading.Platform.Application.UnitTests.Features.RecordAuthAuditEvent;

public sealed class RecordAuthAuditEventHandlerTests
{
    /// <summary>
    /// Trace: FR3, SR1, TR1.
    /// Verifies: a supported sign-in request resolves configuration and commits the complete audit intent.
    /// Expected: the response is recorded and the intent contains the configured environments, summary, severity, and inward data.
    /// Why: the Application slice must own event semantics while persistence remains behind an operation-specific port.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ShouldCommitSignInAuditIntent_WhenRequestIsSupported()
    {
        var fixture = CreateFixture();

        var result = await fixture.Handler.HandleAsync(
            new RecordAuthAuditEventRequest(
                PlatformAuthenticationDefaults.AuditEvents.SignInCompleted,
                null,
                null,
                "preferred.operator",
                "subject-1",
                "correlation-1"),
            CancellationToken.None);

        Assert.True(result.IsRecorded);
        var auditEvent = Assert.Single(fixture.Committer.Intents).Event;
        Assert.Equal(PlatformEnvironmentKind.Live, auditEvent.PlatformEnvironment);
        Assert.Equal(BrokerEnvironmentKind.Demo, auditEvent.BrokerEnvironment);
        Assert.Equal("Information", auditEvent.Severity);
        Assert.Equal("Operator preferred.operator completed sign-in.", auditEvent.Summary);
        Assert.Equal("correlation-1", auditEvent.CorrelationId);
    }

    /// <summary>
    /// Trace: FR3, NF1, TR1.
    /// Verifies: an empty identity value is carried into the inward request without introducing framework identity dependencies.
    /// Expected: the audit event is still recorded with the supplied identity value.
    /// Why: the API owns provider-specific claim fallback, while the Application handler remains a simple-data use case.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ShouldPreserveUnknownIdentityValue_WhenIdentityIsUnavailable()
    {
        var fixture = CreateFixture();

        await fixture.Handler.HandleAsync(
            new RecordAuthAuditEventRequest(
                PlatformAuthenticationDefaults.AuditEvents.SignOutCompleted,
                "/authentication/sign-out",
                null,
                "unknown-operator",
                null,
                "correlation-2"),
            CancellationToken.None);

        Assert.Equal("Operator unknown-operator completed sign-out.", Assert.Single(fixture.Committer.Intents).Event.Summary);
    }

    /// <summary>
    /// Trace: FR3, TR1.
    /// Verifies: configuration lookup failures stop the operation before persistence is requested.
    /// Expected: the configuration exception propagates and the commit port receives no intent.
    /// Why: audit records must not be written with fabricated environment context.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ShouldPropagateConfigurationFailureWithoutWriting_WhenConfigurationIsUnavailable()
    {
        var fixture = CreateFixture();
        fixture.ConfigurationReader.Exception = new InvalidOperationException("configuration unavailable");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Handler.HandleAsync(CreateRequest(), CancellationToken.None));

        Assert.Equal("configuration unavailable", exception.Message);
        Assert.Empty(fixture.Committer.Intents);
    }

    /// <summary>
    /// Trace: FR3, TR1.
    /// Verifies: unsupported event identifiers are rejected before configuration lookup or persistence.
    /// Expected: an argument error propagates and both inward ports remain unused.
    /// Why: invalid audit input must be write-free and must not turn into a misleading operational event.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ShouldRejectUnsupportedEventWithoutWrites_WhenEventTypeIsInvalid()
    {
        var fixture = CreateFixture();

        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Handler.HandleAsync(CreateRequest("UnsupportedEvent"), CancellationToken.None));

        Assert.Equal(0, fixture.ConfigurationReader.CallCount);
        Assert.Empty(fixture.Committer.Intents);
    }

    /// <summary>
    /// Trace: FR3, TR1.
    /// Verifies: cooperative cancellation from configuration lookup is preserved.
    /// Expected: OperationCanceledException propagates and no persistence intent is created.
    /// Why: request cancellation must not be translated into a successful audit response.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ShouldPropagateCancellationWithoutWriting_WhenConfigurationLookupIsCancelled()
    {
        var fixture = CreateFixture();
        fixture.ConfigurationReader.Exception = new OperationCanceledException();

        await Assert.ThrowsAsync<OperationCanceledException>(() => fixture.Handler.HandleAsync(CreateRequest(), CancellationToken.None));

        Assert.Empty(fixture.Committer.Intents);
    }

    /// <summary>
    /// Trace: FR3, TR1.
    /// Verifies: persistence failures are not hidden by the Application handler.
    /// Expected: the committer exception propagates after the intent has been built.
    /// Why: the API host must retain its existing Problem Details translation for unexpected persistence failures.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ShouldPropagatePersistenceFailure_WhenCommitFails()
    {
        var fixture = CreateFixture();
        fixture.Committer.Exception = new InvalidOperationException("persistence failed");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Handler.HandleAsync(CreateRequest(), CancellationToken.None));

        Assert.Equal("persistence failed", exception.Message);
        Assert.Single(fixture.Committer.Intents);
    }

    private static RecordAuthAuditEventRequest CreateRequest(string? eventType = null) =>
        new(eventType ?? PlatformAuthenticationDefaults.AuditEvents.AccessDenied, "/operator/configuration", null, "operator", "subject", "correlation");

    private static Fixture CreateFixture()
    {
        var configurationReader = new FakeConfigurationReader(CreateConfiguration());
        var committer = new FakeCommitter();
        return new Fixture(
            new RecordAuthAuditEventHandler(configurationReader, committer, new FixedTimeProvider()),
            configurationReader,
            committer);
    }

    private static PlatformConfigurationSnapshot CreateConfiguration() => new(
        PlatformEnvironmentKind.Live,
        BrokerEnvironmentKind.Demo,
        new TradingScheduleConfiguration(new TimeOnly(8, 0), new TimeOnly(16, 30), [DayOfWeek.Monday], WeekendBehavior.ExcludeWeekends, [], "UTC"),
        new RetryPolicyConfiguration(1, 5, 2, 60, 5),
        new NotificationSettingsConfiguration("RecordedOnly", "owner@example.com"),
        new CredentialPresence(true, true, true),
        true,
        true,
        DateTimeOffset.UtcNow,
        false);

    private sealed record Fixture(RecordAuthAuditEventHandler Handler, FakeConfigurationReader ConfigurationReader, FakeCommitter Committer);

    private sealed class FakeConfigurationReader(PlatformConfigurationSnapshot configuration) : IRecordAuthAuditEventConfigurationReader
    {
        public int CallCount { get; private set; }
        public Exception? Exception { get; set; }

        public Task<PlatformConfigurationSnapshot> GetCurrentAsync(CancellationToken cancellationToken)
        {
            CallCount++;
            if (Exception is not null)
            {
                throw Exception;
            }

            return Task.FromResult(configuration);
        }
    }

    private sealed class FakeCommitter : IRecordAuthAuditEventCommitter
    {
        public List<RecordAuthAuditEventIntent> Intents { get; } = [];
        public Exception? Exception { get; set; }

        public Task CommitAsync(RecordAuthAuditEventIntent intent, CancellationToken cancellationToken)
        {
            Intents.Add(intent);
            return Exception is null ? Task.CompletedTask : Task.FromException(Exception);
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.Parse("2026-07-27T10:00:00Z");
    }
}