using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

namespace TNC.Trading.Platform.Infrastructure.UnitTests;

internal static class CatalogTestSeed
{
    private static readonly Guid DemoBrokerEnvironmentId = Guid.Parse("D2A0A8C0-9E0F-4A31-9CE8-2D8F2AF2A001");
    private static readonly Guid LiveBrokerEnvironmentId = Guid.Parse("A2B0A8C0-9E0F-4A31-9CE8-2D8F2AF2A002");

    internal static async Task<Guid> SeedAsync(
        PlatformDbContext dbContext,
        BrokerEnvironmentKind kind,
        string endpointProfile,
        CancellationToken cancellationToken = default)
    {
        var brokerEnvironmentId = GetBrokerEnvironmentId(kind);
        var kindName = kind.ToString();
        var name = $"IG {kindName} Unit Test";
        var now = DateTimeOffset.UtcNow;

        dbContext.BrokerEnvironments.Add(new BrokerEnvironmentEntity
        {
            BrokerEnvironmentId = brokerEnvironmentId,
            Name = name,
            NormalizedName = name.ToUpperInvariant(),
            Provider = "IG",
            Kind = kindName,
            Lifecycle = "Active",
            Availability = "Available",
            EndpointProfile = endpointProfile,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        dbContext.BrokerEnvironmentSelections.Add(new BrokerEnvironmentSelectionEntity
        {
            SelectionId = 1,
            SelectedBrokerEnvironmentId = brokerEnvironmentId,
            AppliedBrokerEnvironmentId = brokerEnvironmentId,
            RestartRequired = false,
            Version = 1,
            UpdatedAtUtc = now
        });

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return brokerEnvironmentId;
    }

    internal static Guid GetBrokerEnvironmentId(BrokerEnvironmentKind kind) =>
        kind switch
        {
            BrokerEnvironmentKind.Demo => DemoBrokerEnvironmentId,
            BrokerEnvironmentKind.Live => LiveBrokerEnvironmentId,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Only IG Demo and Live environments can be seeded.")
        };
}
