namespace TNC.Trading.Platform.Web.Components.Layout;

/// <summary>
/// Represents the active platform and broker environment summary displayed in the shared shell.
/// </summary>
public sealed record PlatformShellEnvironment(
    string PlatformEnvironment,
    string BrokerEnvironment,
    bool LiveOptionAvailable);
