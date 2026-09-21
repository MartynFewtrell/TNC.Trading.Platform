namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

internal sealed class AccountPreferencesOperationEntity
{
    public Guid AccountPreferencesOperationId { get; set; }
    public string IdempotencyKey { get; set; } = null!;
    public string PlatformEnvironment { get; set; } = null!;
    public string BrokerEnvironment { get; set; } = null!;
    public string AccountId { get; set; } = null!;
    public long BaselineRevision { get; set; }
    public bool RequestedTrailingStopsEnabled { get; set; }
    public string Actor { get; set; } = null!;
    public string CorrelationId { get; set; } = null!;
    public string Phase { get; set; } = null!;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}