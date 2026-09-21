namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

internal sealed class BrokerEnvironmentSelectionEntity
{
    public int SelectionId { get; set; }
    public Guid? SelectedBrokerEnvironmentId { get; set; }
    public Guid? AppliedBrokerEnvironmentId { get; set; }
    public bool RestartRequired { get; set; }
    public long Version { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}