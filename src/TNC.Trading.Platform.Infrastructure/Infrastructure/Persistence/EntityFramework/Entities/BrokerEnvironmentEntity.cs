namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

internal sealed class BrokerEnvironmentEntity
{
    public Guid BrokerEnvironmentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Lifecycle { get; set; } = string.Empty;
    public string Availability { get; set; } = string.Empty;
    public string? AvailabilityReason { get; set; }
    public string EndpointProfile { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public byte[] ConcurrencyToken { get; set; } = [];
}