namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

internal sealed class IgProviderRateReservationEntity
{
    public Guid RequestId { get; set; }
    public string ScopeType { get; set; } = string.Empty;
    public string ScopeHash { get; set; } = string.Empty;
    public DateTimeOffset ReservedAtUtc { get; set; }
}
