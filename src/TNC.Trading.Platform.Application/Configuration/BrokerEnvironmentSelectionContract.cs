namespace TNC.Trading.Platform.Application.Configuration;

internal sealed record BrokerEnvironmentSelectionContract(
    Guid SelectionId,
    Guid? SelectedBrokerEnvironmentId,
    Guid? AppliedBrokerEnvironmentId,
    bool RestartRequired,
    long Version);