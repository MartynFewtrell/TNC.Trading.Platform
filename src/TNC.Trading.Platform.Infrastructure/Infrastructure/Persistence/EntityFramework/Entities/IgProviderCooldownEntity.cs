namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

internal sealed class IgProviderCooldownEntity
{
    public string ScopeType { get; set; } = string.Empty;
    public string ScopeHash { get; set; } = string.Empty;
    public DateTimeOffset CooldownUntilUtc { get; set; }
}
