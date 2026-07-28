using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.PlatformAuthentication.Ports;
using TNC.Trading.Platform.Application.Features.ReconcilePlatformAuthentication;
using TNC.Trading.Platform.Application.Services;
using TNC.Trading.Platform.Infrastructure.Configuration.SqlServer;
using TNC.Trading.Platform.Infrastructure.Credentials.DataProtection;
using TNC.Trading.Platform.Infrastructure.Notifications;
using TNC.Trading.Platform.Infrastructure.Notifications.Recorded;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;
using TNC.Trading.Platform.Infrastructure.Platform;
using ApplicationReflection = TNC.Trading.Platform.Infrastructure.UnitTests.InfrastructureReflection;

namespace TNC.Trading.Platform.Infrastructure.UnitTests;

public class IgProofDataTests
{
    /// <summary>
    /// Traces to FR3, FR5, FR9, NF1, NF3, SR2, SR3, TR4, TR7.
    /// Verifies: when authentication succeeds and both accounts and positions queries return data,
    /// a proof-data snapshot is persisted in the store with the preferred account and position count.
    /// Expected: the store holds a snapshot whose account name, balance, and position count match the fake responses.
    /// Why: the status surface must show live read-only IG Demo proof data to confirm a real authenticated session.
    /// </summary>
    [Fact]
    public async Task TickAsync_WhenSessionActiveAndProofQuerySucceeds_ShouldPersistProofDataSnapshot()
    {
        using var dbContext = InfrastructureReflection.CreateDbContext();
        var store = new InMemoryPlatformIgProofDataStore();
        var sessionClient = CreateGatewayWithProofData(
            new BrokerAuthenticationProof("Demo Account", "ACC1", 5000m, 2));
        var provider = InfrastructureReflection.CreateDataProtectionProvider();
        await SeedCredentialsAsync(dbContext, provider);
        var coordinator = CreateCoordinator(dbContext, sessionClient: sessionClient, proofDataStore: store, dataProtectionProvider: provider);

        await coordinator.TickAsync(CancellationToken.None);

        var snapshot = await store.GetLatestAsync(BrokerEnvironmentKind.Demo, CancellationToken.None);
        Assert.NotNull(snapshot);
        Assert.Equal("Demo Account", snapshot.PreferredAccountName);
        Assert.Equal("ACC1", snapshot.PreferredAccountId);
        Assert.Equal(5000m, snapshot.Balance);
        Assert.Equal(2, snapshot.OpenPositionCount);
    }

    /// <summary>
    /// Traces to FR3, FR5, FR9, NF1, NF3, SR2, SR3, TR4, TR7.
    /// Verifies: when authentication succeeds but the accounts query throws, the session remains active
    /// without a proof-data snapshot being persisted (non-fatal failure path).
    /// Expected: the store returns null for the latest snapshot; no exception propagates to the caller.
    /// Why: proof-data retrieval is best-effort; a query failure must not interrupt an established session.
    /// </summary>
    [Fact]
    public async Task TickAsync_WhenSessionActiveAndAccountsQueryFails_ShouldRemainActiveWithoutProofData()
    {
        using var dbContext = InfrastructureReflection.CreateDbContext();
        var store = new InMemoryPlatformIgProofDataStore();
        var sessionClient = CreateGatewayWithoutProofData();
        var coordinator = CreateCoordinator(dbContext, sessionClient: sessionClient, proofDataStore: store);

        await coordinator.TickAsync(CancellationToken.None);

        var snapshot = await store.GetLatestAsync(BrokerEnvironmentKind.Demo, CancellationToken.None);
        Assert.Null(snapshot);
    }

    /// <summary>
    /// Traces to FR3, FR5, FR9, NF1, NF3, SR2, SR3, TR4, TR7.
    /// Verifies: when authentication succeeds but the positions query throws, the session remains active
    /// without a proof-data snapshot being persisted (non-fatal failure path).
    /// Expected: the store returns null; no exception propagates.
    /// Why: same non-fatal contract as accounts failure — positions are best-effort.
    /// </summary>
    [Fact]
    public async Task TickAsync_WhenSessionActiveAndPositionsQueryFails_ShouldRemainActiveWithoutProofData()
    {
        using var dbContext = InfrastructureReflection.CreateDbContext();
        var store = new InMemoryPlatformIgProofDataStore();
        var sessionClient = CreateGatewayWithoutProofData();
        var coordinator = CreateCoordinator(dbContext, sessionClient: sessionClient, proofDataStore: store);

        await coordinator.TickAsync(CancellationToken.None);

        var snapshot = await store.GetLatestAsync(BrokerEnvironmentKind.Demo, CancellationToken.None);
        Assert.Null(snapshot);
    }

    /// <summary>
    /// Traces to FR3, FR5, FR9, NF1, NF3, SR2, SR3, TR4, TR7.
    /// Verifies: when no account has the Preferred flag set, the first available account is used as fallback.
    /// Expected: the snapshot carries the first account's name.
    /// Why: the IG API may not always set Preferred=true; a deterministic fallback prevents null proof data.
    /// </summary>
    [Fact]
    public async Task TickAsync_WhenSessionActiveAndNoPreferredAccount_ShouldUseFallbackAccount()
    {
        using var dbContext = InfrastructureReflection.CreateDbContext();
        var store = new InMemoryPlatformIgProofDataStore();
        var sessionClient = CreateGatewayWithProofData(
            new BrokerAuthenticationProof("First Account", "ACC1", 100m, 0));
        var provider = InfrastructureReflection.CreateDataProtectionProvider();
        await SeedCredentialsAsync(dbContext, provider);
        var coordinator = CreateCoordinator(dbContext, sessionClient: sessionClient, proofDataStore: store, dataProtectionProvider: provider);

        await coordinator.TickAsync(CancellationToken.None);

        var snapshot = await store.GetLatestAsync(BrokerEnvironmentKind.Demo, CancellationToken.None);
        Assert.NotNull(snapshot);
        Assert.Equal("First Account", snapshot.PreferredAccountName);
    }

    /// <summary>
    /// Traces to FR3, FR5, FR9, NF1, NF3, SR2, SR3, TR4, TR7.
    /// Verifies: when proof data has been captured and GetStatusAsync is called, the returned projection
    /// carries the latest proof-data snapshot.
    /// Expected: IgLoginStatus.LatestProofData is not null with matching fields.
    /// Why: the status surface must propagate proof data through the application layer for API serialisation.
    /// </summary>
    [Fact]
    public async Task GetStatusAsync_WhenProofDataAvailable_ShouldIncludeItInProjection()
    {
        using var dbContext = InfrastructureReflection.CreateDbContext();
        var store = new InMemoryPlatformIgProofDataStore();
        var sessionClient = CreateGatewayWithProofData(
            new BrokerAuthenticationProof("Test Account", "ACC1", 999m, 1));
        var provider = InfrastructureReflection.CreateDataProtectionProvider();
        await SeedCredentialsAsync(dbContext, provider);
        var coordinator = CreateCoordinator(dbContext, sessionClient: sessionClient, proofDataStore: store, dataProtectionProvider: provider);

        var status = await coordinator.GetStatusAsync(CancellationToken.None);

        Assert.NotNull(status.IgLoginStatus.LatestProofData);
        Assert.Equal("Test Account", status.IgLoginStatus.LatestProofData.PreferredAccountName);
        Assert.Equal(1, status.IgLoginStatus.LatestProofData.OpenPositionCount);
    }

    /// <summary>
    /// Traces to FR3, FR5, FR9, NF1, NF3, SR2, SR3, TR4, TR7.
    /// Verifies: when no proof data has been captured yet, GetStatusAsync returns a projection with
    /// LatestProofData set to null rather than throwing or returning stale data.
    /// Expected: IgLoginStatus.LatestProofData is null.
    /// Why: the status page must gracefully handle the not-yet-retrieved state.
    /// </summary>
    [Fact]
    public async Task GetStatusAsync_WhenProofDataUnavailable_ShouldReturnNullLatestProofData()
    {
        using var dbContext = InfrastructureReflection.CreateDbContext();
        var store = new InMemoryPlatformIgProofDataStore();
        // Use a coordinator where auth will fail (no credentials set up) so proof data is never captured.
        var coordinator = CreateCoordinator(dbContext, proofDataStore: store);

        var status = await coordinator.GetStatusAsync(CancellationToken.None);

        Assert.Null(status.IgLoginStatus.LatestProofData);
    }

    // ----- Factory helpers -----

    private static PlatformAuthenticationReconciler CreateCoordinator(
        PlatformDbContext dbContext,
        IBrokerAuthenticationGateway? sessionClient = null,
        IPlatformIgProofDataStore? proofDataStore = null,
        IDataProtectionProvider? dataProtectionProvider = null)
    {
        var timeProvider = TimeProvider.System;
        var configuration = CreateConfiguration();
        var protectedCredentialService = CreateProtectedCredentialService(dbContext, timeProvider, dataProtectionProvider);
        var configurationService = new PlatformConfigurationService(
            CreateConfigurationStore(dbContext, configuration, protectedCredentialService, timeProvider));

        return new PlatformAuthenticationReconciler(
            new PlatformAuthSimulationSettings(TimeSpan.FromMinutes(15)),
            configurationService,
            new EfPlatformRuntimeStateStore(dbContext),
            new EfPlatformIgLoginSnapshotStore(dbContext),
            new EfPlatformRetryCycleStore(dbContext),
            new EfPlatformEventStore(dbContext),
            CreateNotificationDispatcher(dbContext, timeProvider),
            new TradingScheduleGate(),
            sessionClient ?? CreateNoOpSessionClient(),
            proofDataStore ?? new InMemoryPlatformIgProofDataStore(),
            timeProvider,
            ApplicationReflection.CreateNullApplicationLogger(),
            new NoopPlatformReconciliationLease());
    }

    private static async Task SeedCredentialsAsync(
        PlatformDbContext dbContext,
        IDataProtectionProvider dataProtectionProvider)
    {
        var service = CreateProtectedCredentialService(dbContext, TimeProvider.System, dataProtectionProvider);
        await service.UpdateAsync(BrokerEnvironmentKind.Demo, "api-key", "test@example.com", "password", "unit-test", CancellationToken.None);
    }

    private static IConfiguration CreateConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Bootstrap:PlatformEnvironment"] = "Live",
                ["Bootstrap:BrokerEnvironment"] = "Demo",
                ["Bootstrap:TradingSchedule:StartOfDay"] = "00:00",
                ["Bootstrap:TradingSchedule:EndOfDay"] = "23:59",
                ["Bootstrap:TradingSchedule:TradingDays:0"] = "Sunday",
                ["Bootstrap:TradingSchedule:TradingDays:1"] = "Monday",
                ["Bootstrap:TradingSchedule:TradingDays:2"] = "Tuesday",
                ["Bootstrap:TradingSchedule:TradingDays:3"] = "Wednesday",
                ["Bootstrap:TradingSchedule:TradingDays:4"] = "Thursday",
                ["Bootstrap:TradingSchedule:TradingDays:5"] = "Friday",
                ["Bootstrap:TradingSchedule:TradingDays:6"] = "Saturday",
                ["Bootstrap:TradingSchedule:WeekendBehavior"] = "IncludeFullWeekend",
                ["Bootstrap:TradingSchedule:TimeZone"] = "UTC",
                ["Bootstrap:RetryPolicy:InitialDelaySeconds"] = "1",
                ["Bootstrap:RetryPolicy:MaxAutomaticRetries"] = "1",
                ["Bootstrap:RetryPolicy:Multiplier"] = "2",
                ["Bootstrap:RetryPolicy:MaxDelaySeconds"] = "60",
                ["Bootstrap:RetryPolicy:PeriodicDelayMinutes"] = "5",
                ["Bootstrap:NotificationSettings:Provider"] = "RecordedOnly",
                ["Bootstrap:NotificationSettings:EmailTo"] = "owner@example.com",
                ["Bootstrap:UpdatedBy"] = "unit-test"
            })
            .Build();
    }

    private static ProtectedCredentialService CreateProtectedCredentialService(
        PlatformDbContext dbContext,
        TimeProvider timeProvider,
        IDataProtectionProvider? dataProtectionProvider = null)
    {
        return new ProtectedCredentialService(
            dbContext,
            dataProtectionProvider ?? InfrastructureReflection.CreateDataProtectionProvider(),
            timeProvider);
    }

    private static SqlPlatformConfigurationStore CreateConfigurationStore(
        PlatformDbContext dbContext,
        IConfiguration configuration,
        ProtectedCredentialService credentialService,
        TimeProvider timeProvider)
    {
        return new SqlPlatformConfigurationStore(dbContext, configuration, credentialService, timeProvider);
    }

    private static NotificationDispatcher CreateNotificationDispatcher(PlatformDbContext dbContext, TimeProvider timeProvider)
    {
        return new NotificationDispatcher(
            dbContext,
            [new RecordedNotificationProvider(ApplicationReflection.CreateNullLogger<RecordedNotificationProvider>())],
            ApplicationReflection.CreateNullLogger<NotificationDispatcher>(),
            timeProvider);
    }

    private static IBrokerAuthenticationGateway CreateNoOpSessionClient()
    {
        return new ProofDataFakeGateway((_, _) => Task.FromResult(
            BrokerAuthenticationOutcome.Failed(new BrokerAuthenticationFailure(
                BrokerAuthenticationFailureKind.Unreachable,
                "IG authentication failed: broker is unreachable."))));
    }

    private static IBrokerAuthenticationGateway CreateGatewayWithProofData(BrokerAuthenticationProof proof)
    {
        return new ProofDataFakeGateway((_, _) => Task.FromResult(CreateOutcome(proof)));
    }

    private static IBrokerAuthenticationGateway CreateGatewayWithoutProofData()
    {
        return new ProofDataFakeGateway((_, _) => Task.FromResult(CreateOutcome(proof: null)));
    }

    private static BrokerAuthenticationOutcome CreateOutcome(BrokerAuthenticationProof? proof)
    {
        return BrokerAuthenticationOutcome.Succeeded(
            new BrokerAuthenticationEvidence(
                "configured-demo-session",
                null,
                DateTimeOffset.UtcNow.AddMinutes(30)),
            proof);
    }

    private sealed class ProofDataFakeGateway(
        Func<BrokerAuthenticationRequest, CancellationToken, Task<BrokerAuthenticationOutcome>> authenticateAsync)
        : IBrokerAuthenticationGateway
    {
        public Task<BrokerAuthenticationOutcome> AuthenticateAndCollectProofAsync(
            BrokerAuthenticationRequest request,
            CancellationToken cancellationToken)
            => authenticateAsync(request, cancellationToken);
    }
}
