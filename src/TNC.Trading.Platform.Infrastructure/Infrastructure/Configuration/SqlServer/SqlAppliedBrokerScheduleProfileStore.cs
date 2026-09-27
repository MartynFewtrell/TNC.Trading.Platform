using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.AppliedBrokerSchedule;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;
using TNC.Trading.Platform.Infrastructure.Platform;

namespace TNC.Trading.Platform.Infrastructure.Configuration.SqlServer;

internal sealed class SqlAppliedBrokerScheduleProfileStore(
    PlatformDbContext dbContext,
    IPlatformEnvironmentContext platformEnvironmentContext,
    TimeProvider timeProvider) : IAppliedBrokerScheduleProfileStore
{
    public async Task<AppliedBrokerScheduleProfile> GetAppliedAsync(CancellationToken cancellationToken)
    {
        var profile = await GetAppliedProfileAsync(cancellationToken).ConfigureAwait(false);
        var reconciliationRequired = await IsLegacyReconciliationRequiredAsync(profile, cancellationToken).ConfigureAwait(false);
        return ToContract(profile, reconciliationRequired);
    }

    public async Task<AppliedBrokerScheduleProfile> SaveAppliedAsync(
        TradingScheduleConfiguration tradingSchedule,
        string actor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tradingSchedule);
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);
        var profile = await GetAppliedProfileAsync(cancellationToken).ConfigureAwait(false);
        var reconciliationRequired = await IsLegacyReconciliationRequiredAsync(profile, cancellationToken).ConfigureAwait(false);
        profile.ScheduleVersion = checked(profile.ScheduleVersion + 1);
        profile.TradingHoursStart = tradingSchedule.StartOfDay;
        profile.TradingHoursEnd = tradingSchedule.EndOfDay;
        profile.TradingDaysCsv = string.Join(',', tradingSchedule.TradingDays);
        profile.WeekendBehavior = tradingSchedule.WeekendBehavior.ToString();
        profile.BankHolidayExclusionsJson = JsonSerializer.Serialize(tradingSchedule.BankHolidayExclusions);
        profile.TimeZone = tradingSchedule.TimeZone.Trim();

        var configuration = await dbContext.PlatformConfigurations
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (configuration is not null)
        {
            dbContext.ConfigurationAudits.Add(new ConfigurationAuditEntity
            {
                ConfigurationId = configuration.ConfigurationId,
                PlatformEnvironment = platformEnvironmentContext.Environment.ToString(),
                BrokerEnvironment = profile.BrokerEnvironmentId.ToString("N"),
                OccurredAtUtc = timeProvider.GetUtcNow(),
                ChangedBy = actor,
                ChangeType = reconciliationRequired
                    ? "AppliedBrokerScheduleLegacyReconciled"
                    : "AppliedBrokerScheduleUpdated",
                Summary = reconciliationRequired
                    ? "Operator saved the applied broker Trading Day schedule and completed legacy schedule reconciliation."
                    : "Operator updated the applied broker Trading Day schedule.",
                DetailsJson = JsonSerializer.Serialize(new
                {
                    profile.BrokerEnvironmentId,
                    profile.ScheduleVersion,
                    tradingSchedule.StartOfDay,
                    tradingSchedule.EndOfDay,
                    tradingSchedule.TradingDays,
                    tradingSchedule.WeekendBehavior,
                    tradingSchedule.BankHolidayExclusions,
                    tradingSchedule.TimeZone,
                    LegacyReconciliationRequired = reconciliationRequired
                }),
                CorrelationId = Guid.NewGuid().ToString("N")
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToContract(profile, false);
    }

    private async Task<bool> IsLegacyReconciliationRequiredAsync(
        BrokerEnvironmentScheduleProfileEntity profile,
        CancellationToken cancellationToken)
    {
        var hasRecordedReconciliation = await dbContext.ConfigurationAudits
            .AsNoTracking()
            .AnyAsync(
                item => item.PlatformEnvironment == platformEnvironmentContext.Environment.ToString()
                    && item.BrokerEnvironment == profile.BrokerEnvironmentId.ToString("N")
                    && (item.ChangeType == "AppliedBrokerScheduleLegacyMatched"
                        || item.ChangeType == "AppliedBrokerScheduleLegacyReconciled"),
                cancellationToken)
            .ConfigureAwait(false);
        if (hasRecordedReconciliation)
        {
            return false;
        }

        var legacyConfiguration = await dbContext.PlatformConfigurations
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (legacyConfiguration is null)
        {
            return false;
        }

        TradingScheduleConfiguration legacySchedule;
        try
        {
            legacySchedule = new TradingScheduleConfiguration(
                legacyConfiguration.TradingHoursStart,
                legacyConfiguration.TradingHoursEnd,
                legacyConfiguration.TradingDaysCsv
                    .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .Select(day => Enum.Parse<DayOfWeek>(day, ignoreCase: true))
                    .ToArray(),
                Enum.Parse<WeekendBehavior>(legacyConfiguration.WeekendBehavior, ignoreCase: true),
                JsonSerializer.Deserialize<DateOnly[]>(legacyConfiguration.BankHolidayExclusionsJson) ?? [],
                legacyConfiguration.TimeZone);
        }
        catch (Exception exception) when (exception is ArgumentException or JsonException)
        {
            return true;
        }

        var profileSchedule = ToContract(profile, false).TradingSchedule;
        if (!SchedulesMatch(legacySchedule, profileSchedule))
        {
            return true;
        }

        dbContext.ConfigurationAudits.Add(new ConfigurationAuditEntity
        {
            ConfigurationId = legacyConfiguration.ConfigurationId,
            PlatformEnvironment = platformEnvironmentContext.Environment.ToString(),
            BrokerEnvironment = profile.BrokerEnvironmentId.ToString("N"),
            OccurredAtUtc = timeProvider.GetUtcNow(),
            ChangedBy = "system",
            ChangeType = "AppliedBrokerScheduleLegacyMatched",
            Summary = "The applied broker Trading Day schedule already matched the legacy platform schedule.",
            DetailsJson = JsonSerializer.Serialize(new { profile.BrokerEnvironmentId }),
            CorrelationId = Guid.NewGuid().ToString("N")
        });
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return false;
    }

    private static bool SchedulesMatch(
        TradingScheduleConfiguration left,
        TradingScheduleConfiguration right) =>
        left.StartOfDay == right.StartOfDay
        && left.EndOfDay == right.EndOfDay
        && left.WeekendBehavior == right.WeekendBehavior
        && string.Equals(left.TimeZone, right.TimeZone, StringComparison.Ordinal)
        && left.TradingDays.OrderBy(day => day).SequenceEqual(right.TradingDays.OrderBy(day => day))
        && left.BankHolidayExclusions.OrderBy(date => date).SequenceEqual(right.BankHolidayExclusions.OrderBy(date => date));

    private async Task<BrokerEnvironmentScheduleProfileEntity> GetAppliedProfileAsync(CancellationToken cancellationToken)
    {
        var selection = await dbContext.BrokerEnvironmentSelections
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (selection?.AppliedBrokerEnvironmentId is not { } appliedBrokerEnvironmentId)
        {
            throw new InvalidOperationException("An applied broker environment is required to read or update its Trading Day schedule.");
        }

        return await dbContext.BrokerEnvironmentScheduleProfiles
            .SingleOrDefaultAsync(item => item.BrokerEnvironmentId == appliedBrokerEnvironmentId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The applied broker environment does not have a Trading Day schedule profile.");
    }

    private static AppliedBrokerScheduleProfile ToContract(
        BrokerEnvironmentScheduleProfileEntity profile,
        bool legacyReconciliationRequired)
    {
        try
        {
            var tradingDays = profile.TradingDaysCsv
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(day => Enum.Parse<DayOfWeek>(day, ignoreCase: true))
                .ToArray();
            var holidays = JsonSerializer.Deserialize<DateOnly[]>(profile.BankHolidayExclusionsJson) ?? [];
            var weekendBehavior = Enum.Parse<WeekendBehavior>(profile.WeekendBehavior, ignoreCase: true);
            return new(
                profile.BrokerEnvironmentId,
                profile.DefaultsVersion,
                new TradingScheduleConfiguration(
                    profile.TradingHoursStart,
                    profile.TradingHoursEnd,
                    tradingDays,
                    weekendBehavior,
                    holidays,
                    profile.TimeZone,
                    profile.BrokerEnvironmentId,
                    profile.ScheduleVersion),
                legacyReconciliationRequired,
                profile.ScheduleVersion);
        }
        catch (Exception exception) when (exception is ArgumentException or JsonException)
        {
            throw new InvalidOperationException(
                "The applied broker Trading Day schedule profile is malformed.",
                exception);
        }
    }
}
