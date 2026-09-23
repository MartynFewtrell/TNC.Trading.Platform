namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

internal sealed class BrokerEnvironmentNotificationProfileEntity
{
    public Guid BrokerEnvironmentId { get; set; }
    public int DefaultsVersion { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string? EmailTo { get; set; }
    public bool Enabled { get; set; }
}