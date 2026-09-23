namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

internal sealed class BrokerEnvironmentRetirementTokenEntity
{
    public Guid TokenId { get; set; }
    public Guid BrokerEnvironmentId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public string ConcurrencyToken { get; set; } = string.Empty;
    public string PreviewHash { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public bool IsUsed { get; set; }
}