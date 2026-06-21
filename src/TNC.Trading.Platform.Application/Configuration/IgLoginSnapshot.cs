namespace TNC.Trading.Platform.Application.Configuration;

internal sealed record IgLoginSnapshot(
    Guid Id,
    BrokerEnvironmentKind BrokerEnvironment,
    DateTimeOffset CapturedAtUtc,
    DateOnly TradingDay,
    IgLoginSnapshotKind SnapshotKind,
    string CurrentAccountId,
    string? LightstreamerEndpoint,
    DateTimeOffset? SessionExpiresAtUtc,
    IReadOnlyDictionary<string, string> ResponseHeaders,
    string RawNonSecretPayloadJson);
