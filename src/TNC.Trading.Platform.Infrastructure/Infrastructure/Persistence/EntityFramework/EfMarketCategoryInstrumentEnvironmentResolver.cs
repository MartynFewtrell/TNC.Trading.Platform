using Microsoft.EntityFrameworkCore;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;

internal static class EfMarketCategoryInstrumentEnvironmentResolver
{
    internal static async Task VerifyAppliedAtCommitAsync(
        PlatformDbContext dbContext,
        IAppliedBrokerEnvironmentContextResolver? contextResolver,
        Guid expectedEnvironmentId,
        BrokerEnvironmentKind expectedEnvironment,
        string? expectedEndpointProfile,
        CancellationToken cancellationToken,
        bool requireExecutable = true)
    {
        if (!dbContext.Database.IsRelational())
        {
            return;
        }

        if (contextResolver is not null)
        {
            var applied = await contextResolver.ResolveAppliedAsync(cancellationToken).ConfigureAwait(false);
            if (applied is null
                || applied.BrokerEnvironmentId != expectedEnvironmentId
                || !string.Equals(applied.Provider, "IG", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(applied.Kind, expectedEnvironment.ToString(), StringComparison.Ordinal)
                || (requireExecutable && !applied.IsExecutable)
                || (expectedEndpointProfile is not null
                    && !string.Equals(applied.EndpointProfile, expectedEndpointProfile, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException("The applied IG environment or endpoint profile changed before persistence could commit.");
            }

            return;
        }

        var selectedEnvironmentId = await dbContext.BrokerEnvironmentSelections
            .Where(item => item.SelectionId == 1)
            .Select(item => item.AppliedBrokerEnvironmentId)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        var environment = await dbContext.BrokerEnvironments.AsNoTracking()
            .Where(item => item.BrokerEnvironmentId == expectedEnvironmentId)
            .Select(item => new { item.Provider, item.Kind, item.Lifecycle, item.Availability, item.EndpointProfile })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (selectedEnvironmentId != expectedEnvironmentId
            || environment is null
            || !string.Equals(environment.Provider, "IG", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(environment.Kind, expectedEnvironment.ToString(), StringComparison.Ordinal)
            || (requireExecutable
                && (!string.Equals(environment.Lifecycle, "Active", StringComparison.Ordinal)
                    || !string.Equals(environment.Availability, "Available", StringComparison.Ordinal)))
            || (expectedEndpointProfile is not null
                && !string.Equals(environment.EndpointProfile, expectedEndpointProfile, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("The applied IG environment or endpoint profile changed before persistence could commit.");
        }
    }

    internal static async Task<Guid> ResolveAppliedIdAsync(
        PlatformDbContext dbContext,
        IAppliedBrokerEnvironmentContextResolver? contextResolver,
        BrokerEnvironmentKind expectedEnvironment,
        CancellationToken cancellationToken,
        bool requireExecutable = true)
    {
        AppliedBrokerEnvironmentContext? applied;
        if (contextResolver is not null)
        {
            applied = await contextResolver.ResolveAppliedAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var appliedId = await dbContext.BrokerEnvironmentSelections
                .Where(item => item.SelectionId == 1)
                .Select(item => item.AppliedBrokerEnvironmentId)
                .SingleOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            applied = appliedId is null
                ? null
                : await dbContext.BrokerEnvironments.AsNoTracking()
                    .Where(item => item.BrokerEnvironmentId == appliedId.Value)
                    .Select(item => new AppliedBrokerEnvironmentContext(
                        item.BrokerEnvironmentId,
                        item.Provider,
                        item.Kind,
                        item.Lifecycle,
                        item.Availability,
                        item.EndpointProfile,
                        item.Availability == "Available"))
                    .SingleOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);
        }

        if (applied is null
            || !string.Equals(applied.Provider, "IG", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(applied.Kind, expectedEnvironment.ToString(), StringComparison.Ordinal)
            || (requireExecutable && !applied.IsExecutable))
        {
            throw new InvalidOperationException("The applied IG broker environment is unavailable or does not match the requested environment.");
        }

        return applied.BrokerEnvironmentId;
    }
}
