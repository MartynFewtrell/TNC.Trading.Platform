namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

internal sealed class BrokerEnvironmentRetryProfileEntity
{
    public Guid BrokerEnvironmentId { get; set; }
    public int DefaultsVersion { get; set; }
    public int InitialDelaySeconds { get; set; }
    public int MaxAutomaticRetries { get; set; }
    public int Multiplier { get; set; }
    public int MaxDelaySeconds { get; set; }
    public int PeriodicDelayMinutes { get; set; }
}