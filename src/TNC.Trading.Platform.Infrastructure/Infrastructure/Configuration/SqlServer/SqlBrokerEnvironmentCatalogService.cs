using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.BrokerEnvironments;
using TNC.Trading.Platform.Infrastructure.Credentials.DataProtection;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

namespace TNC.Trading.Platform.Infrastructure.Configuration.SqlServer;

internal sealed class SqlBrokerEnvironmentCatalogService(
    PlatformDbContext dbContext,
    ProtectedCredentialService credentialService,
    IPlatformEnvironmentContext platformEnvironmentContext,
    TimeProvider timeProvider) : IBrokerEnvironmentCatalogService
{
    public async Task<IReadOnlyList<BrokerEnvironmentCatalogItem>> ListAsync(CancellationToken cancellationToken)
    {
        var environments = await dbContext.BrokerEnvironments
            .AsNoTracking()
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = new List<BrokerEnvironmentCatalogItem>(environments.Count);
        foreach (var environment in environments)
        {
            items.Add(await ToItemAsync(environment, cancellationToken).ConfigureAwait(false));
        }

        return items;
    }

    public async Task<BrokerEnvironmentOperationResult> CreateAsync(CreateBrokerEnvironmentCommand command, CancellationToken cancellationToken)
    {
        var normalizedName = command.Name.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalizedName) || string.IsNullOrWhiteSpace(command.EndpointProfile))
        {
            return new(false, "Name and approved endpoint profile are required.");
        }

        if (await dbContext.BrokerEnvironments.AnyAsync(item => item.NormalizedName == normalizedName, cancellationToken).ConfigureAwait(false))
        {
            return new(false, "A broker environment with this name already exists.");
        }

        var defaults = await dbContext.BrokerEnvironmentDefaults.SingleAsync(item => item.IsActive, cancellationToken).ConfigureAwait(false);
        var id = Guid.NewGuid();
        var now = timeProvider.GetUtcNow();
        var entity = new BrokerEnvironmentEntity
        {
            BrokerEnvironmentId = id, Name = command.DisplayName.Trim(), NormalizedName = normalizedName,
            Provider = command.Provider.Trim(), Kind = command.Kind.Trim(), Lifecycle = "Draft", Availability = "Unavailable",
            AvailabilityReason = "Capability has not been approved for this catalog record.", EndpointProfile = command.EndpointProfile.Trim(),
            CreatedAtUtc = now, UpdatedAtUtc = now
        };
        dbContext.BrokerEnvironments.Add(entity);
        dbContext.InstrumentCollectionSettings.Add(new InstrumentCollectionSettingsEntity
        {
            BrokerEnvironmentId = id,
            CurrentUpdatesPerDay = 1
        });
        dbContext.BrokerEnvironmentScheduleProfiles.Add(new BrokerEnvironmentScheduleProfileEntity { BrokerEnvironmentId = id, DefaultsVersion = defaults.Version, TradingHoursStart = defaults.TradingHoursStart, TradingHoursEnd = defaults.TradingHoursEnd, TradingDaysCsv = defaults.TradingDaysCsv, WeekendBehavior = defaults.WeekendBehavior, BankHolidayExclusionsJson = defaults.BankHolidayExclusionsJson, TimeZone = defaults.TimeZone });
        dbContext.BrokerEnvironmentRetryProfiles.Add(new BrokerEnvironmentRetryProfileEntity { BrokerEnvironmentId = id, DefaultsVersion = defaults.Version, InitialDelaySeconds = defaults.RetryInitialDelaySeconds, MaxAutomaticRetries = defaults.RetryMaxAutomaticRetries, Multiplier = defaults.RetryMultiplier, MaxDelaySeconds = defaults.RetryMaxDelaySeconds, PeriodicDelayMinutes = defaults.RetryPeriodicDelayMinutes });
        dbContext.BrokerEnvironmentNotificationProfiles.Add(new BrokerEnvironmentNotificationProfileEntity { BrokerEnvironmentId = id, DefaultsVersion = defaults.Version, Provider = defaults.NotificationProvider, EmailTo = defaults.NotificationEmailTo, Enabled = false });
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new(true, null, ToItem(entity, false));
    }

    public async Task<BrokerEnvironmentOperationResult> SaveCredentialsAsync(SaveBrokerEnvironmentCredentialsCommand command, CancellationToken cancellationToken)
    {
        var entity = await dbContext.BrokerEnvironments.SingleOrDefaultAsync(item => item.BrokerEnvironmentId == command.BrokerEnvironmentId, cancellationToken).ConfigureAwait(false);
        if (entity is null) return new(false, "Broker environment was not found.");
        if (entity.Lifecycle is "Retired" or "Unavailable" || !IsSupportedIgCredentialProfile(entity.Provider, entity.Kind, entity.EndpointProfile)) return new(false, "This broker environment cannot authenticate.");
        if (string.IsNullOrWhiteSpace(command.ApiKey) && string.IsNullOrWhiteSpace(command.Identifier) && string.IsNullOrWhiteSpace(command.Password)) return new(false, "At least one credential must be supplied.");

        await credentialService.UpdateCatalogAsync(command.BrokerEnvironmentId, command.ApiKey, command.Identifier, command.Password, command.Actor, cancellationToken).ConfigureAwait(false);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new(true, null, ToItem(entity, true));
    }

    public async Task<BrokerEnvironmentOperationResult> SelectAsync(SelectBrokerEnvironmentCommand command, CancellationToken cancellationToken)
    {
        if (!command.Acknowledged) return new(false, "Restart acknowledgement is required.");
        var target = await dbContext.BrokerEnvironments.SingleOrDefaultAsync(item => item.BrokerEnvironmentId == command.BrokerEnvironmentId, cancellationToken).ConfigureAwait(false);
        if (target is null || target.Lifecycle is "Retired" || target.Availability != "Available") return new(false, target?.AvailabilityReason ?? "Broker environment is unavailable.");
        var selection = await dbContext.BrokerEnvironmentSelections.SingleAsync(cancellationToken).ConfigureAwait(false);
        if (selection.Version != command.ExpectedRevision) return new(false, "Selection has changed. Refresh and try again.");
        selection.SelectedBrokerEnvironmentId = target.BrokerEnvironmentId;
        selection.RestartRequired = selection.AppliedBrokerEnvironmentId != target.BrokerEnvironmentId;
        selection.Version++;
        selection.UpdatedAtUtc = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new(true, null, Status: await GetStatusAsync(cancellationToken).ConfigureAwait(false));
    }

    public async Task<BrokerEnvironmentStatus> GetStatusAsync(CancellationToken cancellationToken)
    {
        var selection = await dbContext.BrokerEnvironmentSelections.AsNoTracking().SingleAsync(cancellationToken).ConfigureAwait(false);
        var ids = new[] { selection.AppliedBrokerEnvironmentId, selection.SelectedBrokerEnvironmentId }.Where(item => item.HasValue).Select(item => item!.Value).Distinct().ToArray();
        var records = await dbContext.BrokerEnvironments.AsNoTracking().Where(item => ids.Contains(item.BrokerEnvironmentId)).ToDictionaryAsync(item => item.BrokerEnvironmentId, cancellationToken).ConfigureAwait(false);
        var appliedItem = selection.AppliedBrokerEnvironmentId is { } applied && records.TryGetValue(applied, out var appliedRecord)
            ? await ToItemAsync(appliedRecord, cancellationToken).ConfigureAwait(false)
            : null;
        var selectedItem = selection.SelectedBrokerEnvironmentId is { } selected && records.TryGetValue(selected, out var selectedRecord)
            ? await ToItemAsync(selectedRecord, cancellationToken).ConfigureAwait(false)
            : null;

        return new(platformEnvironmentContext.Environment.ToString(), appliedItem, selectedItem, selection.RestartRequired, selection.Version);
    }

    public async Task<BrokerEnvironmentRetirementPreview?> PreviewRetirementAsync(Guid brokerEnvironmentId, string actor, CancellationToken cancellationToken)
    {
        var entity = await dbContext.BrokerEnvironments.SingleOrDefaultAsync(item => item.BrokerEnvironmentId == brokerEnvironmentId, cancellationToken).ConfigureAwait(false);
        if (entity is null || entity.Lifecycle == "Retired") return null;
        var selection = await dbContext.BrokerEnvironmentSelections.AsNoTracking().SingleAsync(cancellationToken).ConfigureAwait(false);
        if (selection.AppliedBrokerEnvironmentId == brokerEnvironmentId || selection.SelectedBrokerEnvironmentId == brokerEnvironmentId) return null;

        var purge = await GetCountsAsync(brokerEnvironmentId, cancellationToken).ConfigureAwait(false);
        var retained = await GetRetainedCountsAsync(entity, brokerEnvironmentId, cancellationToken).ConfigureAwait(false);
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var expires = timeProvider.GetUtcNow().AddMinutes(5);
        var payload = BuildPreviewPayload(brokerEnvironmentId, entity.Name, entity.NormalizedName, Convert.ToBase64String(entity.ConcurrencyToken), purge, retained);
        dbContext.BrokerEnvironmentRetirementTokens.Add(new BrokerEnvironmentRetirementTokenEntity
        {
            TokenId = Guid.NewGuid(), BrokerEnvironmentId = brokerEnvironmentId, TokenHash = Hash(token), Actor = actor,
            ConcurrencyToken = Convert.ToBase64String(entity.ConcurrencyToken), PreviewHash = Hash(payload), ExpiresAtUtc = expires
        });
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new(brokerEnvironmentId, entity.Name, entity.NormalizedName, Convert.ToBase64String(entity.ConcurrencyToken), token, expires, purge, retained);
    }

    public async Task<BrokerEnvironmentRetirementResult> RetireAsync(RetireBrokerEnvironmentCommand command, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var entity = await dbContext.BrokerEnvironments.SingleOrDefaultAsync(item => item.BrokerEnvironmentId == command.BrokerEnvironmentId, cancellationToken).ConfigureAwait(false);
        var token = await dbContext.BrokerEnvironmentRetirementTokens.SingleOrDefaultAsync(item => item.TokenHash == Hash(command.ConfirmationToken), cancellationToken).ConfigureAwait(false);
        var selection = await dbContext.BrokerEnvironmentSelections.AsNoTracking().SingleAsync(cancellationToken).ConfigureAwait(false);
        if (entity is null || token is null || token.ExpiresAtUtc <= timeProvider.GetUtcNow() || token.BrokerEnvironmentId != command.BrokerEnvironmentId || token.Actor != command.Actor || token.ConcurrencyToken != command.ExpectedConcurrencyToken || selection.AppliedBrokerEnvironmentId == command.BrokerEnvironmentId || selection.SelectedBrokerEnvironmentId == command.BrokerEnvironmentId || entity.Lifecycle == "Retired")
            return new(false, "Retirement confirmation is invalid, expired, stale, or the environment is selected or applied.");
        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(entity.NormalizedName), Encoding.UTF8.GetBytes(command.TypedName)))
            return new(false, "The typed environment name does not match.");
        if (Convert.ToBase64String(entity.ConcurrencyToken) != command.ExpectedConcurrencyToken)
            return new(false, "The broker environment changed. Refresh and preview again.");

        var purge = await GetCountsAsync(command.BrokerEnvironmentId, cancellationToken).ConfigureAwait(false);
        var retained = await GetRetainedCountsAsync(entity, command.BrokerEnvironmentId, cancellationToken).ConfigureAwait(false);
        var currentPayload = BuildPreviewPayload(command.BrokerEnvironmentId, entity.Name, entity.NormalizedName, command.ExpectedConcurrencyToken, purge, retained);
        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(token.PreviewHash), Encoding.UTF8.GetBytes(Hash(currentPayload))))
            return new(false, "Retirement preview data changed. Refresh and preview again.");

        var claimed = await dbContext.BrokerEnvironmentRetirementTokens
            .Where(item => item.TokenId == token.TokenId && !item.IsUsed)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.IsUsed, true), cancellationToken)
            .ConfigureAwait(false);
        if (claimed != 1)
            return new(false, "Retirement confirmation has already been used.");

        await dbContext.ProtectedCredentials.Where(item => item.BrokerEnvironmentId == command.BrokerEnvironmentId).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await dbContext.BrokerEnvironmentScheduleProfiles.Where(item => item.BrokerEnvironmentId == command.BrokerEnvironmentId).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await dbContext.BrokerEnvironmentRetryProfiles.Where(item => item.BrokerEnvironmentId == command.BrokerEnvironmentId).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await dbContext.BrokerEnvironmentNotificationProfiles.Where(item => item.BrokerEnvironmentId == command.BrokerEnvironmentId).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await dbContext.AuthRuntimeStates.Where(item => item.BrokerEnvironmentId == command.BrokerEnvironmentId).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await dbContext.AuthRetryCycles.Where(item => item.BrokerEnvironmentId == command.BrokerEnvironmentId).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await dbContext.IgProofData.Where(item => item.BrokerEnvironmentId == command.BrokerEnvironmentId).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await dbContext.IgLoginSnapshots.Where(item => item.BrokerEnvironmentId == command.BrokerEnvironmentId).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await dbContext.AccountPreferencesCurrentStates.Where(item => item.BrokerEnvironmentId == command.BrokerEnvironmentId).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await dbContext.AccountPreferencesOperations.Where(item => item.BrokerEnvironmentId == command.BrokerEnvironmentId).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await dbContext.MarketDetailCurrent.Where(item => item.BrokerEnvironmentId == command.BrokerEnvironmentId).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await dbContext.MarketDetailEligibility.Where(item => item.BrokerEnvironmentId == command.BrokerEnvironmentId).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await dbContext.MarketDetailCollectionRuns
            .Where(item => item.BrokerEnvironmentId == command.BrokerEnvironmentId && (item.Status == "Collecting" || item.Status == "Paused"))
            .ExecuteUpdateAsync(update => update
                .SetProperty(item => item.Status, "Superseded")
                .SetProperty(item => item.LeaseOwner, (Guid?)null)
                .SetProperty(item => item.LeaseExpiresAtUtc, (DateTimeOffset?)null)
                .SetProperty(item => item.SafeReasonCode, "EnvironmentRetired")
                .SetProperty(item => item.UpdatedAtUtc, timeProvider.GetUtcNow()), cancellationToken)
            .ConfigureAwait(false);
        entity.Lifecycle = "Retired";
        entity.Availability = "Unavailable";
        entity.AvailabilityReason = "Retired by an administrator.";
        entity.UpdatedAtUtc = timeProvider.GetUtcNow();
        dbContext.BrokerEnvironmentRetirementAudits.Add(new BrokerEnvironmentRetirementAuditEntity
        {
            BrokerEnvironmentId = entity.BrokerEnvironmentId, Name = entity.Name, Actor = command.Actor, OccurredAtUtc = timeProvider.GetUtcNow(),
            PurgeCountsJson = JsonSerializer.Serialize(purge), RetainedCountsJson = JsonSerializer.Serialize(retained)
        });
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new(true, null, purge, retained);
    }

    private async Task<Dictionary<string, int>> GetCountsAsync(Guid id, CancellationToken cancellationToken) => new()
    {
        ["ProtectedCredentials"] = await dbContext.ProtectedCredentials.CountAsync(item => item.BrokerEnvironmentId == id, cancellationToken).ConfigureAwait(false),
        ["ScheduleProfiles"] = await dbContext.BrokerEnvironmentScheduleProfiles.CountAsync(item => item.BrokerEnvironmentId == id, cancellationToken).ConfigureAwait(false),
        ["RetryProfiles"] = await dbContext.BrokerEnvironmentRetryProfiles.CountAsync(item => item.BrokerEnvironmentId == id, cancellationToken).ConfigureAwait(false),
        ["NotificationProfiles"] = await dbContext.BrokerEnvironmentNotificationProfiles.CountAsync(item => item.BrokerEnvironmentId == id, cancellationToken).ConfigureAwait(false),
        ["RuntimeStates"] = await dbContext.AuthRuntimeStates.CountAsync(item => item.BrokerEnvironmentId == id, cancellationToken).ConfigureAwait(false),
        ["RetryCycles"] = await dbContext.AuthRetryCycles.CountAsync(item => item.BrokerEnvironmentId == id, cancellationToken).ConfigureAwait(false),
        ["ProofData"] = await dbContext.IgProofData.CountAsync(item => item.BrokerEnvironmentId == id, cancellationToken).ConfigureAwait(false),
        ["LoginSnapshots"] = await dbContext.IgLoginSnapshots.CountAsync(item => item.BrokerEnvironmentId == id, cancellationToken).ConfigureAwait(false),
        ["AccountPreferencesCurrentState"] = await dbContext.AccountPreferencesCurrentStates.CountAsync(item => item.BrokerEnvironmentId == id, cancellationToken).ConfigureAwait(false),
        ["AccountPreferencesOperations"] = await dbContext.AccountPreferencesOperations.CountAsync(item => item.BrokerEnvironmentId == id, cancellationToken).ConfigureAwait(false),
        ["MarketDetailCurrent"] = await dbContext.MarketDetailCurrent.CountAsync(item => item.BrokerEnvironmentId == id, cancellationToken).ConfigureAwait(false),
        ["MarketDetailEligibility"] = await dbContext.MarketDetailEligibility.CountAsync(item => item.BrokerEnvironmentId == id, cancellationToken).ConfigureAwait(false)
    };

    private async Task<Dictionary<string, int>> GetRetainedCountsAsync(BrokerEnvironmentEntity entity, Guid id, CancellationToken cancellationToken) => new()
    {
        ["ConfigurationAudits"] = await dbContext.ConfigurationAudits.CountAsync(item => item.BrokerEnvironment == entity.Name, cancellationToken).ConfigureAwait(false),
        ["OperationalEvents"] = await dbContext.OperationalEvents.CountAsync(item => item.BrokerEnvironment == entity.Name, cancellationToken).ConfigureAwait(false),
        ["NotificationRecords"] = await dbContext.NotificationRecords.CountAsync(item => item.BrokerEnvironment == entity.Name, cancellationToken).ConfigureAwait(false),
        ["AccountRetrievals"] = await dbContext.AccountDetailsRetrievals.CountAsync(item => item.BrokerEnvironmentId == id, cancellationToken).ConfigureAwait(false),
        ["AccountDetailsAccounts"] = await dbContext.AccountDetailsAccounts.CountAsync(item => item.Retrieval.BrokerEnvironmentId == id, cancellationToken).ConfigureAwait(false),
        ["PreferenceObservations"] = await dbContext.TrailingStopsPreferenceObservations.CountAsync(item => item.BrokerEnvironmentId == id, cancellationToken).ConfigureAwait(false),
        ["DesiredStateAudits"] = await dbContext.AccountPreferencesDesiredStateAudits.CountAsync(item => item.BrokerEnvironmentId == id, cancellationToken).ConfigureAwait(false),
        ["MarketDetailCollectionRuns"] = await dbContext.MarketDetailCollectionRuns.CountAsync(item => item.BrokerEnvironmentId == id, cancellationToken).ConfigureAwait(false),
        ["MarketDetailRunSources"] = await dbContext.MarketDetailRunSources.CountAsync(item => item.BrokerEnvironmentId == id, cancellationToken).ConfigureAwait(false),
        ["MarketDetailRunTargets"] = await dbContext.MarketDetailRunTargets.CountAsync(item => item.BrokerEnvironmentId == id, cancellationToken).ConfigureAwait(false),
        ["MarketDetailRunMemberships"] = await dbContext.MarketDetailRunMemberships.CountAsync(item => item.BrokerEnvironmentId == id, cancellationToken).ConfigureAwait(false),
        ["MarketDetailObservations"] = await dbContext.MarketDetailObservations.CountAsync(item => item.BrokerEnvironmentId == id, cancellationToken).ConfigureAwait(false)
    };

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string BuildPreviewPayload(Guid id, string name, string normalizedName, string concurrencyToken, IReadOnlyDictionary<string, int> purge, IReadOnlyDictionary<string, int> retained)
        => JsonSerializer.Serialize(new { brokerEnvironmentId = id, name, normalizedName, concurrencyToken, purge, retained });

    private static bool IsIgDemoEnvironment(string provider, string kind) =>
        string.Equals(provider, "IG", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(kind, "Demo", StringComparison.OrdinalIgnoreCase);

    private static bool IsSupportedIgCredentialProfile(string provider, string kind, string endpointProfile) =>
        string.Equals(provider, "IG", StringComparison.OrdinalIgnoreCase) &&
        ((string.Equals(kind, "Demo", StringComparison.OrdinalIgnoreCase)
          && string.Equals(endpointProfile, "IgDemo", StringComparison.Ordinal))
         || (string.Equals(kind, "Live", StringComparison.OrdinalIgnoreCase)
             && string.Equals(endpointProfile, "IgLive", StringComparison.Ordinal)));

    private async Task<BrokerEnvironmentCatalogItem> ToItemAsync(BrokerEnvironmentEntity item, CancellationToken cancellationToken)
    {
        var credentialPresence = await credentialService
            .GetPresenceAsync(item.BrokerEnvironmentId, cancellationToken)
            .ConfigureAwait(false);

        return ToItem(item, credentialPresence.IsAuthenticationReady);
    }

    private static BrokerEnvironmentCatalogItem ToItem(BrokerEnvironmentEntity item, bool hasCredentials) => new(item.BrokerEnvironmentId, item.Name, item.Provider, item.Kind, item.Lifecycle, item.Availability, item.AvailabilityReason, item.EndpointProfile, IsIgDemoEnvironment(item.Provider, item.Kind), hasCredentials, item.ConcurrencyToken.Length == 0 ? null : Convert.ToBase64String(item.ConcurrencyToken));
}