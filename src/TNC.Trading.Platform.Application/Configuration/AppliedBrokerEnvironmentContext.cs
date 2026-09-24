namespace TNC.Trading.Platform.Application.Configuration;

internal sealed record AppliedBrokerEnvironmentContext(
    Guid BrokerEnvironmentId,
    string Provider,
    string Kind,
    string Lifecycle,
    string Availability,
    string EndpointProfile,
    bool CanAuthenticate,
    bool CanAccessMarketData = false)
{
    public bool IsExecutable => Lifecycle == "Active" && Availability == "Available" && CanAuthenticate;
}