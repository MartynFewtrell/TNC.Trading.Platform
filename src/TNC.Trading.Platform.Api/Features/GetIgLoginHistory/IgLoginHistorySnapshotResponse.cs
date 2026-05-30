namespace TNC.Trading.Platform.Api.Features.GetIgLoginHistory;

internal sealed record IgLoginHistorySnapshotResponse(
    Guid SnapshotId,
    DateTimeOffset CapturedAtUtc,
    DateOnly TradingDay,
    string CurrentAccountId,
    string? LightstreamerEndpoint,
    DateTimeOffset? SessionExpiresAtUtc,
    IReadOnlyDictionary<string, string> ResponseHeaders,
    string RawNonSecretPayloadJson);
