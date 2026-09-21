namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

internal sealed class TrailingStopsPreferenceObservationEntity
{
    public Guid TrailingStopsPreferenceObservationId { get; set; }

    public string BrokerEnvironment { get; set; } = null!;

    public string PlatformEnvironment { get; set; } = null!;

    public string? AccountId { get; set; }

    public bool TrailingStopsEnabled { get; set; }

    public DateTimeOffset ObservedAtUtc { get; set; }

    public DateTimeOffset RecordedAtUtc { get; set; }

    public string ObservationKind { get; set; } = null!;

    public string Source { get; set; } = null!;

    public string? Actor { get; set; }

    public string CorrelationId { get; set; } = null!;
}