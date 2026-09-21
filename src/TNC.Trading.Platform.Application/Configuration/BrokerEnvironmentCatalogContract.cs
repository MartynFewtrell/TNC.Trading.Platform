namespace TNC.Trading.Platform.Application.Configuration;

internal sealed record BrokerEnvironmentCatalogContract(
    Guid BrokerEnvironmentId,
    string Name,
    string NormalizedName,
    BrokerEnvironmentProvider Provider,
    BrokerEnvironmentKind Kind,
    BrokerEnvironmentLifecycle Lifecycle,
    BrokerEnvironmentAvailability Availability,
    string? AvailabilityReason,
    string EndpointProfile);