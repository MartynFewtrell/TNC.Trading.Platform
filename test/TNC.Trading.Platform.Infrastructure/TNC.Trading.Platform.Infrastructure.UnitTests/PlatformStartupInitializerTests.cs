using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Services;
using TNC.Trading.Platform.Infrastructure.Operations.Retention;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;
using TNC.Trading.Platform.Infrastructure.Startup;

namespace TNC.Trading.Platform.Infrastructure.UnitTests;

public sealed class PlatformStartupInitializerTests
{
    /// <summary>
    /// Trace: Clean Architecture Migration Phase 7, Step 7.2.
    /// Verifies: startup applies schema creation before bootstrap configuration and invokes retention afterward.
    /// Expected: the configuration step can write after schema creation and its expired row is then removed.
    /// Why: API startup must preserve the blocking Infrastructure lifecycle order and must not skip retention.
    /// </summary>
    [Fact]
    public async Task InitializeAsync_ShouldApplyStepsInRequiredOrder_WhenApiStarts()
    {
        var now = new DateTimeOffset(2026, 7, 27, 12, 0, 0, TimeSpan.Zero);
        await using var dbContext = InfrastructureReflection.CreateDbContext();
        var configurationStore = new RecordingConfigurationStore(dbContext, now);
        var initializer = CreateInitializer(dbContext, configurationStore, now);

        await initializer.InitializeAsync(CancellationToken.None);

        Assert.True(configurationStore.SchemaWasAvailable);
        Assert.Equal(1, configurationStore.ApplyStartupConfigurationCallCount);
        Assert.Empty(dbContext.OperationalEvents);
    }

    /// <summary>
    /// Trace: Clean Architecture Migration Phase 7, Step 7.2.
    /// Verifies: schema initialization failure aborts the startup surface before configuration or retention can run.
    /// Expected: a clear initialization exception is returned and the configuration store remains untouched.
    /// Why: readiness must remain unhealthy when required Infrastructure initialization fails.
    /// </summary>
    [Fact]
    public async Task InitializeAsync_ShouldFailReadiness_WhenMigrationFails()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>().Options;
        await using var dbContext = new PlatformDbContext(options);
        var configurationStore = new RecordingConfigurationStore(dbContext, DateTimeOffset.UtcNow);
        var initializer = CreateInitializer(dbContext, configurationStore, DateTimeOffset.UtcNow);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => initializer.InitializeAsync(CancellationToken.None));

        Assert.Contains("schema initialization failed", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, configurationStore.ApplyStartupConfigurationCallCount);
    }

    /// <summary>
    /// Trace: Clean Architecture Migration Phase 7, Step 7.2.
    /// Verifies: cancellation from a required startup step is preserved by the Infrastructure surface.
    /// Expected: the cancellation exception reaches the caller without being converted into a successful startup.
    /// Why: host shutdown or startup cancellation must not leave the API reporting readiness after incomplete initialization.
    /// </summary>
    [Fact]
    public async Task InitializeAsync_ShouldPropagateCancellation_WhenBootstrapIsCancelled()
    {
        var cancellationSource = new CancellationTokenSource();
        await using var dbContext = InfrastructureReflection.CreateDbContext();
        var configurationStore = new RecordingConfigurationStore(dbContext, DateTimeOffset.UtcNow)
        {
            CancellationSource = cancellationSource
        };
        var initializer = CreateInitializer(dbContext, configurationStore, DateTimeOffset.UtcNow);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => initializer.InitializeAsync(cancellationSource.Token));
    }

    /// <summary>
    /// Trace: Clean Architecture Migration Phase 7, Step 7.2.
    /// Verifies: the API-facing Infrastructure startup extension exposes only the application and cancellation token.
    /// Expected: no public parameter or return type reveals PlatformDbContext.
    /// Why: API composition must invoke startup without taking ownership of persistence mechanics.
    /// </summary>
    [Fact]
    public void InitializePlatformAsync_ShouldNotExposeDbContext_ToApiComposition()
    {
        var method = typeof(PlatformStartupExtensions).GetMethod(nameof(PlatformStartupExtensions.InitializePlatformAsync));

        Assert.NotNull(method);
        Assert.DoesNotContain(typeof(PlatformDbContext), method!.GetParameters().Select(parameter => parameter.ParameterType));
        Assert.NotEqual(typeof(PlatformDbContext), method.ReturnType);
    }

    private static PlatformStartupInitializer CreateInitializer(
        PlatformDbContext dbContext,
        RecordingConfigurationStore configurationStore,
        DateTimeOffset now)
    {
        var configurationService = new PlatformConfigurationService(configurationStore);
        var retentionConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Retention:OperationalRecordsDays"] = "90"
            })
            .Build();
        var retentionProcessor = new OperationalRecordRetentionProcessor(
            dbContext,
            retentionConfiguration,
            new FixedTimeProvider(now),
            NullLogger<OperationalRecordRetentionProcessor>.Instance);

        return new PlatformStartupInitializer(
            dbContext,
            configurationService,
            retentionProcessor,
            new TestHostEnvironment(),
            NullLogger<PlatformStartupInitializer>.Instance);
    }

    private sealed class RecordingConfigurationStore(PlatformDbContext dbContext, DateTimeOffset now) : IPlatformConfigurationStore
    {
        public bool SchemaWasAvailable { get; private set; }

        public int ApplyStartupConfigurationCallCount { get; private set; }

        public CancellationTokenSource? CancellationSource { get; init; }

        public async Task<PlatformConfigurationSnapshot> ApplyStartupConfigurationAsync(CancellationToken cancellationToken)
        {
            ApplyStartupConfigurationCallCount++;
            SchemaWasAvailable = await dbContext.Database.CanConnectAsync(cancellationToken);
            if (CancellationSource is not null)
            {
                throw new OperationCanceledException(CancellationSource.Token);
            }

            dbContext.OperationalEvents.Add(new OperationalEventEntity
            {
                OccurredAtUtc = now.AddDays(-91),
                Category = "Startup",
                EventType = "Expired",
                PlatformEnvironment = "Test",
                BrokerEnvironment = "Demo",
                Summary = "Expired startup test event",
                DetailsJson = "{}"
            });
            await dbContext.SaveChangesAsync(cancellationToken);
            return CreateSnapshot(now);
        }

        public Task<PlatformConfigurationSnapshot> GetCurrentAsync(CancellationToken cancellationToken) =>
            Task.FromResult(CreateSnapshot(now));

        public Task<PlatformConfigurationSnapshot> GetRuntimeAsync(
            PlatformEnvironmentKind? platformEnvironment,
            BrokerEnvironmentKind? brokerEnvironment,
            CancellationToken cancellationToken) =>
            Task.FromResult(CreateSnapshot(now));

        private static PlatformConfigurationSnapshot CreateSnapshot(DateTimeOffset now) => new(
            PlatformEnvironmentKind.Test,
            BrokerEnvironmentKind.Demo,
            new TradingScheduleConfiguration(
                new TimeOnly(8),
                new TimeOnly(17),
                [DayOfWeek.Monday],
                WeekendBehavior.ExcludeWeekends,
                [],
                "UTC"),
            new RetryPolicyConfiguration(1, 5, 2, 60, 5),
            new NotificationSettingsConfiguration("Recorded", null),
            new CredentialPresence(false, false, false),
            true,
            false,
            now,
            false);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "Infrastructure.UnitTests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}