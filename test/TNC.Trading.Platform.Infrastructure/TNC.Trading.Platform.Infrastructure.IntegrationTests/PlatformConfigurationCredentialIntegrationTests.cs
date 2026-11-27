using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.BrokerEnvironments;
using TNC.Trading.Platform.Application.Features.GetPlatformConfiguration;
using TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;
using TNC.Trading.Platform.Application.Features.ReconcilePlatformAuthentication;
using TNC.Trading.Platform.Application.Features.UpdatePlatformConfiguration;
using TNC.Trading.Platform.Application.Services;
using TNC.Trading.Platform.Infrastructure.Configuration.SqlServer;
using TNC.Trading.Platform.Infrastructure.Credentials.DataProtection;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;
using TNC.Trading.Platform.Infrastructure.Platform;
using TNC.Trading.Platform.Infrastructure.Startup;

namespace TNC.Trading.Platform.Infrastructure.IntegrationTests;

[Collection("SQL Server")]
public sealed class PlatformConfigurationCredentialIntegrationTests(SqlServerDatabaseFixture fixture)
{
    /// <summary>
    /// Verifies a first Configuration save stores all three protected credentials against the applied catalog ID.
    /// Expected: SQL contains exactly three Demo-scoped rows, readiness is true, the values decrypt, and the audit contains no secret material.
    /// Why: this guards the fresh-database foreign-key regression and prevents plaintext secret disclosure through audit history.
    /// </summary>
    [Fact]
    public async Task CommitAsync_ShouldPersistCatalogScopedCredentials_WhenFirstSaveOnFreshDatabase()
    {
        var (context, demoId) = await CreateInitializedDatabaseAsync();
        await using var dbContext = context;
        using var dataProtectionProvider = PlatformConfigurationIntegrationTestHelpers.CreateDataProtectionProvider();
        var store = PlatformConfigurationIntegrationTestHelpers.CreateConfigurationStore(
            dbContext,
            PlatformConfigurationIntegrationTestHelpers.CreateConfiguration(),
            dataProtectionProvider.Provider);

        var result = await store.CommitAsync(
            CreateUpdate("first-api-key", "first-identifier", "first-password"),
            fixture.CancellationToken);

        var credentials = await dbContext.ProtectedCredentials.AsNoTracking().ToListAsync(fixture.CancellationToken);
        Assert.Equal(3, credentials.Count);
        Assert.All(credentials, item => Assert.Equal(demoId, item.BrokerEnvironmentId));
        Assert.DoesNotContain(credentials, item => item.BrokerEnvironmentId == Guid.Empty);
        Assert.True(result.Snapshot.Credentials.IsAuthenticationReady);

        var decrypted = await new ProtectedCredentialService(dbContext, dataProtectionProvider.Provider, TimeProvider.System)
            .GetCredentialsAsync(demoId, fixture.CancellationToken);
        Assert.Equal("first-api-key", decrypted.ApiKey);
        Assert.Equal("first-identifier", decrypted.Identifier);
        Assert.Equal("first-password", decrypted.Password);

        var audit = await dbContext.ConfigurationAudits.AsNoTracking().SingleAsync(fixture.CancellationToken);
        Assert.DoesNotContain("first-api-key", audit.DetailsJson, StringComparison.Ordinal);
        Assert.DoesNotContain("first-identifier", audit.DetailsJson, StringComparison.Ordinal);
        Assert.DoesNotContain("first-password", audit.DetailsJson, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies a partial first save can be completed later without replacing omitted credential values.
    /// Expected: the first save remains incomplete and the subsequent save creates a complete ready credential set.
    /// Why: operators commonly stage missing credentials across separate edits; partial protected state must remain valid.
    /// </summary>
    [Fact]
    public async Task CommitAsync_ShouldCompleteCredentialSet_WhenMissingFieldsAreSavedLater()
    {
        var (context, demoId) = await CreateInitializedDatabaseAsync();
        await using var dbContext = context;
        using var dataProtectionProvider = PlatformConfigurationIntegrationTestHelpers.CreateDataProtectionProvider();
        var store = PlatformConfigurationIntegrationTestHelpers.CreateConfigurationStore(
            dbContext,
            PlatformConfigurationIntegrationTestHelpers.CreateConfiguration(),
            dataProtectionProvider.Provider);

        var firstSave = await store.CommitAsync(CreateUpdate("partial-api-key", null, null), fixture.CancellationToken);
        Assert.False(firstSave.Snapshot.Credentials.IsAuthenticationReady);
        Assert.Equal(1, await dbContext.ProtectedCredentials.CountAsync(fixture.CancellationToken));

        var completedSave = await store.CommitAsync(
            CreateUpdate(null, "completed-identifier", "completed-password"),
            fixture.CancellationToken);

        Assert.Equal(3, await dbContext.ProtectedCredentials.CountAsync(item => item.BrokerEnvironmentId == demoId, fixture.CancellationToken));
        Assert.True(completedSave.Snapshot.Credentials.IsAuthenticationReady);
    }

    /// <summary>
    /// Verifies a password-only update rotates that protected value without changing the other credential ciphertext.
    /// Expected: API key and identifier ciphertexts are byte-for-byte unchanged and the password decrypts to its replacement.
    /// Why: blank write-only form fields mean preserve, not delete or re-encrypt, the existing secret.
    /// </summary>
    [Fact]
    public async Task CommitAsync_ShouldRotateOnlySuppliedSecret_WhenOtherFieldsAreBlank()
    {
        var (context, demoId) = await CreateInitializedDatabaseAsync();
        await using var dbContext = context;
        using var dataProtectionProvider = PlatformConfigurationIntegrationTestHelpers.CreateDataProtectionProvider();
        var store = PlatformConfigurationIntegrationTestHelpers.CreateConfigurationStore(
            dbContext,
            PlatformConfigurationIntegrationTestHelpers.CreateConfiguration(),
            dataProtectionProvider.Provider);
        await store.CommitAsync(CreateUpdate("stable-api-key", "stable-identifier", "old-password"), fixture.CancellationToken);
        var before = await dbContext.ProtectedCredentials.AsNoTracking()
            .Where(item => item.BrokerEnvironmentId == demoId)
            .ToDictionaryAsync(item => item.CredentialType, item => item.ProtectedValue, fixture.CancellationToken);

        await store.CommitAsync(CreateUpdate(string.Empty, "  ", "rotated-password"), fixture.CancellationToken);

        var after = await dbContext.ProtectedCredentials.AsNoTracking()
            .Where(item => item.BrokerEnvironmentId == demoId)
            .ToDictionaryAsync(item => item.CredentialType, item => item.ProtectedValue, fixture.CancellationToken);
        Assert.Equal(before["ApiKey"], after["ApiKey"]);
        Assert.Equal(before["Identifier"], after["Identifier"]);
        var decrypted = await new ProtectedCredentialService(dbContext, dataProtectionProvider.Provider, TimeProvider.System)
            .GetCredentialsAsync(demoId, fixture.CancellationToken);
        Assert.Equal("rotated-password", decrypted.Password);
    }

    /// <summary>
    /// Verifies a migration-backfilled credential row is found by catalog identity and updated rather than duplicated.
    /// Expected: the API key row retains its original credential ID, decrypts to the replacement, and is not duplicated after the Configuration commit.
    /// Why: deployments with legacy rows must transition to canonical catalog ownership without creating split or duplicate secret records.
    /// </summary>
    [Fact]
    public async Task CommitAsync_ShouldUpdateBackfilledRowInPlace_WhenLegacyScopedCredentialExists()
    {
        var (context, demoId) = await CreateInitializedDatabaseAsync();
        await using var dbContext = context;
        using var dataProtectionProvider = PlatformConfigurationIntegrationTestHelpers.CreateDataProtectionProvider();
        var protector = dataProtectionProvider.Provider.CreateProtector("Platform.IgCredentials");
        var legacyCredential = new ProtectedCredentialEntity
        {
            BrokerEnvironmentId = demoId,
            BrokerEnvironment = "Demo",
            CredentialType = "ApiKey",
            ProtectedValue = protector.Protect("backfilled-api-key"),
            ProtectionKind = "DataProtection",
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedBy = "migration"
        };
        dbContext.ProtectedCredentials.Add(legacyCredential);
        await dbContext.SaveChangesAsync(fixture.CancellationToken);
        var credentialId = legacyCredential.CredentialId;
        var store = PlatformConfigurationIntegrationTestHelpers.CreateConfigurationStore(
            dbContext,
            PlatformConfigurationIntegrationTestHelpers.CreateConfiguration(),
            dataProtectionProvider.Provider);

        await store.CommitAsync(CreateUpdate("replacement-api-key", null, null), fixture.CancellationToken);

        var apiKey = await dbContext.ProtectedCredentials.AsNoTracking()
            .SingleAsync(item => item.BrokerEnvironmentId == demoId && item.CredentialType == "ApiKey", fixture.CancellationToken);
        Assert.Equal(credentialId, apiKey.CredentialId);
        Assert.Equal(1, await dbContext.ProtectedCredentials.CountAsync(item => item.BrokerEnvironmentId == demoId && item.CredentialType == "ApiKey", fixture.CancellationToken));
        var decrypted = await new ProtectedCredentialService(dbContext, dataProtectionProvider.Provider, TimeProvider.System)
            .GetCredentialsAsync(demoId, fixture.CancellationToken);
        Assert.Equal("replacement-api-key", decrypted.ApiKey);
    }

    /// <summary>
    /// Verifies Configuration credential edits follow the applied catalog target rather than a different pending selection.
    /// Expected: all three rows belong to Demo, while the distinct selected environment receives no credentials.
    /// Why: a pending operator selection must not redirect secret writes away from the environment used by current runtime gateways.
    /// </summary>
    [Fact]
    public async Task CommitAsync_ShouldTargetAppliedEnvironment_WhenSelectedEnvironmentDiffers()
    {
        var (context, demoId) = await CreateInitializedDatabaseAsync();
        await using var dbContext = context;
        var selectedId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        dbContext.BrokerEnvironments.Add(new BrokerEnvironmentEntity
        {
            BrokerEnvironmentId = selectedId,
            Name = "IG Pending Demo",
            NormalizedName = "IG PENDING DEMO",
            Provider = "Ig",
            Kind = "Demo",
            Lifecycle = "Active",
            Availability = "Available",
            EndpointProfile = "IgDemo",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        var selection = await dbContext.BrokerEnvironmentSelections.SingleAsync(fixture.CancellationToken);
        selection.SelectedBrokerEnvironmentId = selectedId;
        selection.AppliedBrokerEnvironmentId = demoId;
        selection.RestartRequired = true;
        await dbContext.SaveChangesAsync(fixture.CancellationToken);

        using var dataProtectionProvider = PlatformConfigurationIntegrationTestHelpers.CreateDataProtectionProvider();
        var store = PlatformConfigurationIntegrationTestHelpers.CreateConfigurationStore(
            dbContext,
            PlatformConfigurationIntegrationTestHelpers.CreateConfiguration(),
            dataProtectionProvider.Provider);
        await store.CommitAsync(CreateUpdate("applied-api-key", "applied-identifier", "applied-password"), fixture.CancellationToken);

        Assert.Equal(3, await dbContext.ProtectedCredentials.CountAsync(item => item.BrokerEnvironmentId == demoId, fixture.CancellationToken));
        Assert.False(await dbContext.ProtectedCredentials.AnyAsync(item => item.BrokerEnvironmentId == selectedId, fixture.CancellationToken));
    }

    /// <summary>
    /// Verifies unsupported applied environments reject credential replacements before any credential or audit write.
    /// Expected: nonblank credential input raises validation without writes or configuration changes, while empty and whitespace inputs are treated as omissions.
    /// Why: validation must be safe and atomic when the applied target cannot authenticate with IG credentials.
    /// </summary>
    [Fact]
    public async Task CommitAsync_ShouldRejectCredentials_WhenAppliedEnvironmentCannotStoreThem()
    {
        var (context, demoId) = await CreateInitializedDatabaseAsync();
        await using var dbContext = context;
        var applied = await dbContext.BrokerEnvironments.SingleAsync(item => item.BrokerEnvironmentId == demoId, fixture.CancellationToken);
        applied.Lifecycle = "Retired";
        await dbContext.SaveChangesAsync(fixture.CancellationToken);
        using var dataProtectionProvider = PlatformConfigurationIntegrationTestHelpers.CreateDataProtectionProvider();
        var store = PlatformConfigurationIntegrationTestHelpers.CreateConfigurationStore(
            dbContext,
            PlatformConfigurationIntegrationTestHelpers.CreateConfiguration(),
            dataProtectionProvider.Provider);
        _ = await store.GetCurrentAsync(fixture.CancellationToken);
        var originalConfiguration = await dbContext.PlatformConfigurations.AsNoTracking().SingleAsync(fixture.CancellationToken);
        var auditCount = await dbContext.ConfigurationAudits.CountAsync(fixture.CancellationToken);

        var validationException = await Assert.ThrowsAsync<ConfigurationValidationException>(() =>
            store.CommitAsync(CreateUpdate("invalid-api-key", "invalid-identifier", "invalid-password"), fixture.CancellationToken));

        Assert.Equal(
            "The applied broker environment cannot store IG credentials. Ask an Administrator to correct the catalog eligibility or apply a supported IG environment, then retry on the Configuration page. The Broker Environments page is Administrator-only and also rejects credential saves for ineligible environments.",
            Assert.Single(validationException.Errors["Credentials"]));
        var configurationAfterRejection = await dbContext.PlatformConfigurations.AsNoTracking().SingleAsync(fixture.CancellationToken);
        Assert.Equal(
            new
            {
                originalConfiguration.ConfigurationId,
                originalConfiguration.PlatformEnvironment,
                originalConfiguration.BrokerEnvironment,
                originalConfiguration.TradingHoursStart,
                originalConfiguration.TradingHoursEnd,
                originalConfiguration.TradingDaysCsv,
                originalConfiguration.WeekendBehavior,
                originalConfiguration.BankHolidayExclusionsJson,
                originalConfiguration.TimeZone,
                originalConfiguration.RetryInitialDelaySeconds,
                originalConfiguration.RetryMaxAutomaticRetries,
                originalConfiguration.RetryMultiplier,
                originalConfiguration.RetryMaxDelaySeconds,
                originalConfiguration.RetryPeriodicDelayMinutes,
                originalConfiguration.NotificationProvider,
                originalConfiguration.NotificationEmailTo,
                originalConfiguration.UpdatedAtUtc,
                originalConfiguration.UpdatedBy,
                originalConfiguration.RestartRequired
            },
            new
            {
                configurationAfterRejection.ConfigurationId,
                configurationAfterRejection.PlatformEnvironment,
                configurationAfterRejection.BrokerEnvironment,
                configurationAfterRejection.TradingHoursStart,
                configurationAfterRejection.TradingHoursEnd,
                configurationAfterRejection.TradingDaysCsv,
                configurationAfterRejection.WeekendBehavior,
                configurationAfterRejection.BankHolidayExclusionsJson,
                configurationAfterRejection.TimeZone,
                configurationAfterRejection.RetryInitialDelaySeconds,
                configurationAfterRejection.RetryMaxAutomaticRetries,
                configurationAfterRejection.RetryMultiplier,
                configurationAfterRejection.RetryMaxDelaySeconds,
                configurationAfterRejection.RetryPeriodicDelayMinutes,
                configurationAfterRejection.NotificationProvider,
                configurationAfterRejection.NotificationEmailTo,
                configurationAfterRejection.UpdatedAtUtc,
                configurationAfterRejection.UpdatedBy,
                configurationAfterRejection.RestartRequired
            });
        Assert.Empty(await dbContext.ProtectedCredentials.AsNoTracking().ToListAsync(fixture.CancellationToken));
        Assert.Equal(auditCount, await dbContext.ConfigurationAudits.CountAsync(fixture.CancellationToken));
        var settingsOnlyResult = await store.CommitAsync(CreateUpdate(string.Empty, "  ", "\t"), fixture.CancellationToken);
        Assert.NotNull(settingsOnlyResult);
        Assert.Empty(await dbContext.ProtectedCredentials.AsNoTracking().ToListAsync(fixture.CancellationToken));
        Assert.Equal(auditCount + 1, await dbContext.ConfigurationAudits.CountAsync(fixture.CancellationToken));
    }

    /// <summary>
    /// Verifies credentials saved through the Broker Environments catalog path are visible in Configuration metadata.
    /// Expected: current configuration reports all credentials present and authentication-ready for the applied Demo catalog ID.
    /// Why: configuration readiness and runtime credential storage must share the same catalog identity.
    /// </summary>
    [Fact]
    public async Task GetCurrentAsync_ShouldReportCatalogSavedCredentials_WhenSavedThroughBrokerEnvironmentCatalog()
    {
        var (context, demoId) = await CreateInitializedDatabaseAsync();
        await using var dbContext = context;
        using var dataProtectionProvider = PlatformConfigurationIntegrationTestHelpers.CreateDataProtectionProvider();
        var credentialService = new ProtectedCredentialService(dbContext, dataProtectionProvider.Provider, TimeProvider.System);
        var catalogService = new SqlBrokerEnvironmentCatalogService(
            dbContext,
            credentialService,
            new TestPlatformEnvironmentContext(),
            TimeProvider.System);

        var saved = await catalogService.SaveCredentialsAsync(
            new SaveBrokerEnvironmentCredentialsCommand(demoId, "catalog-api-key", "catalog-identifier", "catalog-password", "integration-test"),
            fixture.CancellationToken);
        Assert.True(saved.Succeeded);

        var configurationStore = PlatformConfigurationIntegrationTestHelpers.CreateConfigurationStore(
            dbContext,
            PlatformConfigurationIntegrationTestHelpers.CreateConfiguration(),
            dataProtectionProvider.Provider);
        var snapshot = await configurationStore.GetCurrentAsync(fixture.CancellationToken);

        Assert.True(snapshot.Credentials.HasApiKey);
        Assert.True(snapshot.Credentials.HasIdentifier);
        Assert.True(snapshot.Credentials.HasPassword);
        Assert.True(snapshot.Credentials.IsAuthenticationReady);
    }

    /// <summary>
    /// Verifies GET configuration remains usable when the SQL catalog has no broker selection row.
    /// Expected: the real handler returns a configuration with all credential-presence flags false and marks applied-environment details unavailable.
    /// Why: missing selection is a valid no-target state and must not turn a configuration read into a database exception.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ShouldReturnConfigurationWithoutCredentials_WhenBrokerSelectionIsMissing()
    {
        var (context, _) = await CreateInitializedDatabaseAsync();
        await using var dbContext = context;
        using var dataProtectionProvider = PlatformConfigurationIntegrationTestHelpers.CreateDataProtectionProvider();
        var store = PlatformConfigurationIntegrationTestHelpers.CreateConfigurationStore(
            dbContext,
            PlatformConfigurationIntegrationTestHelpers.CreateConfiguration(),
            dataProtectionProvider.Provider);
        await dbContext.BrokerEnvironmentSelections.ExecuteDeleteAsync(fixture.CancellationToken);
        var resolver = new SqlAppliedBrokerEnvironmentContextResolver(dbContext);
        var handler = new GetPlatformConfigurationHandler(
            new PlatformConfigurationService(store),
            resolver,
            new TestFrequencyReader());

        var response = await handler.HandleAsync(new GetPlatformConfigurationRequest(), fixture.CancellationToken);

        Assert.False(response.Configuration.Credentials.HasApiKey);
        Assert.False(response.Configuration.Credentials.HasIdentifier);
        Assert.False(response.Configuration.Credentials.HasPassword);
        Assert.Equal("AppliedEnvironmentUnavailable", response.InstrumentCollectionSettingsStatus);
    }

    /// <summary>
    /// Verifies settings-only PUT completes through the real handler when the broker selection row is missing.
    /// Expected: settings and audit commit successfully, credentials remain absent, and the response reports unavailable applied-environment details.
    /// Why: a post-commit response read must not misreport a successfully persisted settings update as a failed save.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ShouldCommitSettingsOnlyUpdate_WhenBrokerSelectionIsMissing()
    {
        var (context, _) = await CreateInitializedDatabaseAsync();
        await using var dbContext = context;
        using var dataProtectionProvider = PlatformConfigurationIntegrationTestHelpers.CreateDataProtectionProvider();
        var store = PlatformConfigurationIntegrationTestHelpers.CreateConfigurationStore(
            dbContext,
            PlatformConfigurationIntegrationTestHelpers.CreateConfiguration(),
            dataProtectionProvider.Provider);
        await dbContext.BrokerEnvironmentSelections.ExecuteDeleteAsync(fixture.CancellationToken);
        var resolver = new SqlAppliedBrokerEnvironmentContextResolver(dbContext);
        var handler = new UpdatePlatformConfigurationHandler(
            store,
            new ReconcilePlatformAuthenticationHandler(new TestAuthenticationReconciler()),
            new UpdatePlatformConfigurationValidator(),
            resolver,
            new TestFrequencyReader());

        var response = await handler.HandleAsync(
            new UpdatePlatformConfigurationRequest(CreateUpdate(null, null, null)),
            fixture.CancellationToken);

        Assert.Equal("integration-test", (await dbContext.PlatformConfigurations.AsNoTracking().SingleAsync(fixture.CancellationToken)).UpdatedBy);
        Assert.False(response.Result.Snapshot.Credentials.HasApiKey);
        Assert.False(response.Result.Snapshot.Credentials.HasIdentifier);
        Assert.False(response.Result.Snapshot.Credentials.HasPassword);
        Assert.Equal("AppliedEnvironmentUnavailable", response.InstrumentCollectionSettingsStatus);
        Assert.Single(await dbContext.ConfigurationAudits.AsNoTracking().ToListAsync(fixture.CancellationToken));
    }

    private async Task<(PlatformDbContext Context, Guid DemoId)> CreateInitializedDatabaseAsync()
    {
        await fixture.ResetDatabaseAsync();
        var dbContext = fixture.CreateDbContext();
        await dbContext.Database.MigrateAsync(fixture.CancellationToken);
        var integrityService = new BrokerEnvironmentCatalogIntegrityService(
            dbContext,
            TimeProvider.System,
            NullLogger<BrokerEnvironmentCatalogIntegrityService>.Instance);
        await integrityService.EnsureRequiredCatalogAsync(fixture.CancellationToken);
        var demoId = await SqlServerDatabaseFixture.GetIgDemoBrokerEnvironmentIdAsync(dbContext, fixture.CancellationToken);
        return (dbContext, demoId);
    }

    private static PlatformConfigurationUpdate CreateUpdate(string? apiKey, string? identifier, string? password) =>
        PlatformConfigurationIntegrationTestHelpers.CreateConfigurationUpdate(
            brokerEnvironment: "Demo",
            provider: "RecordedOnly",
            emailTo: "operator@example.test",
            apiKey,
            identifier,
            password,
            changedBy: "integration-test");

    private sealed class TestFrequencyReader : IMarketCategoryInstrumentFrequencyReader
    {
        public Task<MarketCategoryInstrumentFrequency> ReadAsync(
            BrokerEnvironmentKind appliedBrokerEnvironment,
            CancellationToken cancellationToken) =>
            Task.FromResult(new MarketCategoryInstrumentFrequency(1, null, null));
    }

    private sealed class TestAuthenticationReconciler : IPlatformAuthenticationReconciler
    {
        public Task<PlatformRuntimeState> ReconcileAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new PlatformRuntimeState { SessionStatus = PlatformSessionStatus.Active });
    }

    private sealed class TestPlatformEnvironmentContext : IPlatformEnvironmentContext
    {
        public PlatformEnvironmentKind Environment => PlatformEnvironmentKind.Test;
    }
}
