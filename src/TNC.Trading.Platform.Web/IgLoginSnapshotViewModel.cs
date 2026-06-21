namespace TNC.Trading.Platform.Web;

internal sealed record IgLoginSnapshotViewModel(
    Guid SnapshotId,
    DateTimeOffset CapturedAtUtc,
    DateOnly TradingDay,
    string CurrentAccountId,
    string? LightstreamerEndpoint,
    DateTimeOffset? SessionExpiresAtUtc,
    IReadOnlyDictionary<string, string> ResponseHeaders,
    string RawNonSecretPayloadJson);
