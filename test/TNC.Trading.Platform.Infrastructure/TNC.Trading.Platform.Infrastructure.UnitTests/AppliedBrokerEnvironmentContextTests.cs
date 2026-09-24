using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Infrastructure.Configuration.SqlServer;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

namespace TNC.Trading.Platform.Infrastructure.UnitTests;

public sealed class AppliedBrokerEnvironmentContextTests
{
    /// <summary>
    /// Trace: catalog integrity remediation.
    /// Verifies: an active, available IG Demo catalog record is executable when the provider supports authentication.
    /// Expected: the valid seeded catalog state is not blocked before IG authentication.
    /// Why: lifecycle and availability are independent dimensions and the seed uses Active rather than Available lifecycle.
    /// </summary>
    [Fact]
    public void IsExecutable_ShouldReturnTrue_WhenLifecycleIsActiveAndAvailabilityIsAvailable()
    {
        var context = new AppliedBrokerEnvironmentContext(
            Guid.NewGuid(), "Ig", "Demo", "Active", "Available", "IgDemo", true);

        Assert.True(context.IsExecutable);
    }

    /// <summary>
    /// Trace: catalog integrity remediation.
    /// Verifies: availability alone does not make a non-active broker catalog record executable.
    /// Expected: a draft entry remains blocked even when its availability was set incorrectly.
    /// Why: only active catalog entries are permitted to invoke IG provider operations.
    /// </summary>
    [Fact]
    public void IsExecutable_ShouldReturnFalse_WhenLifecycleIsNotActive()
    {
        var context = new AppliedBrokerEnvironmentContext(
            Guid.NewGuid(), "Ig", "Demo", "Draft", "Available", "IgDemo", true);

        Assert.False(context.IsExecutable);
    }

    /// <summary>
    /// Trace: IG Demo catalog authentication.
    /// Verifies: the applied catalog resolver recognizes the title-cased provider and kind values persisted for the seeded IG Demo environment.
    /// Expected: the resolved environment can authenticate and is executable.
    /// Why: a case-sensitive provider check incorrectly blocked authentication before the newly saved credentials could be used.
    /// </summary>
    [Fact]
    public async Task ResolveAppliedAsync_ShouldEnableAuthentication_WhenAppliedEnvironmentUsesSeededIgCasing()
    {
        await using var dbContext = InfrastructureReflection.CreateDbContext();
        var environmentId = Guid.NewGuid();
        dbContext.BrokerEnvironments.Add(new BrokerEnvironmentEntity
        {
            BrokerEnvironmentId = environmentId,
            Name = "IG Demo",
            NormalizedName = "IG DEMO",
            Provider = "Ig",
            Kind = "Demo",
            Lifecycle = "Active",
            Availability = "Available",
            EndpointProfile = "IgDemo",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        dbContext.BrokerEnvironmentSelections.Add(new BrokerEnvironmentSelectionEntity
        {
            SelectionId = 1,
            AppliedBrokerEnvironmentId = environmentId,
            SelectedBrokerEnvironmentId = environmentId,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var context = await new SqlAppliedBrokerEnvironmentContextResolver(dbContext)
            .ResolveAppliedAsync(CancellationToken.None);

        Assert.NotNull(context);
        Assert.True(context.CanAuthenticate);
        Assert.True(context.IsExecutable);
        Assert.True(context.CanAccessMarketData);
    }

    /// <summary>
    /// Trace: Market Category Instruments Work Item 3. Verifies the applied IG Live market-data capability is resolved independently from the existing authentication capability.
    /// Expected: the supported Live endpoint profile can be used for market discovery while CanAuthenticate and IsExecutable remain false.
    /// Why: adding Live reference-data access must not alter order/trading authorization or implicitly enable Live authentication.
    /// </summary>
    [Fact]
    public async Task ResolveAppliedAsync_ShouldSeparateLiveMarketDataCapabilityFromAuthentication_WhenLiveProfileIsApplied()
    {
        await using var dbContext = InfrastructureReflection.CreateDbContext();
        var environmentId = Guid.NewGuid();
        dbContext.BrokerEnvironments.Add(new BrokerEnvironmentEntity
        {
            BrokerEnvironmentId = environmentId,
            Name = "IG Live Discovery",
            NormalizedName = "IG LIVE DISCOVERY",
            Provider = "IG",
            Kind = "Live",
            Lifecycle = "Active",
            Availability = "Available",
            EndpointProfile = "IgLive",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        dbContext.BrokerEnvironmentSelections.Add(new BrokerEnvironmentSelectionEntity
        {
            SelectionId = 1,
            AppliedBrokerEnvironmentId = environmentId,
            SelectedBrokerEnvironmentId = environmentId,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var context = await new SqlAppliedBrokerEnvironmentContextResolver(dbContext)
            .ResolveAppliedAsync(CancellationToken.None);

        Assert.NotNull(context);
        Assert.False(context.CanAuthenticate);
        Assert.False(context.IsExecutable);
        Assert.True(context.CanAccessMarketData);
    }
}
