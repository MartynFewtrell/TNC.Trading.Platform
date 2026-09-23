namespace TNC.Trading.Platform.Application.Features.BrokerEnvironments;

public sealed record BrokerEnvironmentCatalogItem(
    Guid Id,
    string Name,
    string Provider,
    string Kind,
    string Lifecycle,
    string Availability,
    string? AvailabilityReason,
    string EndpointProfile,
    bool CanAuthenticate,
    bool HasCredentials,
    string? ConcurrencyToken);

public sealed record BrokerEnvironmentStatus(
    string PlatformEnvironment,
    BrokerEnvironmentCatalogItem? Applied,
    BrokerEnvironmentCatalogItem? Selected,
    bool RestartRequired,
    long Revision);

public sealed record CreateBrokerEnvironmentCommand(
    string Name,
    string DisplayName,
    string Provider,
    string Kind,
    string EndpointProfile,
    string Actor);

public sealed record SaveBrokerEnvironmentCredentialsCommand(
    Guid BrokerEnvironmentId,
    string? ApiKey,
    string? Identifier,
    string? Password,
    string Actor);

public sealed record SelectBrokerEnvironmentCommand(
    Guid BrokerEnvironmentId,
    long ExpectedRevision,
    bool Acknowledged,
    string Actor);

public sealed record BrokerEnvironmentOperationResult(bool Succeeded, string? Error, BrokerEnvironmentCatalogItem? Item = null, BrokerEnvironmentStatus? Status = null);

public sealed record BrokerEnvironmentRetirementPreview(Guid BrokerEnvironmentId, string Name, string NormalizedName, string ConcurrencyToken, string ConfirmationToken, DateTimeOffset ExpiresAtUtc, IReadOnlyDictionary<string, int> PurgeCounts, IReadOnlyDictionary<string, int> RetainedCounts);
public sealed record RetireBrokerEnvironmentCommand(Guid BrokerEnvironmentId, string ConfirmationToken, string ExpectedConcurrencyToken, string TypedName, string Actor);
public sealed record BrokerEnvironmentRetirementResult(bool Succeeded, string? Error, IReadOnlyDictionary<string, int>? PurgeCounts = null, IReadOnlyDictionary<string, int>? RetainedCounts = null);