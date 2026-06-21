namespace TNC.Trading.Platform.Api.Features.GetPlatformStatus;

internal sealed record IgLoginSnapshotResponse(
    Guid SnapshotId,
    DateTimeOffset CapturedAtUtc,
    DateOnly TradingDay,
    string CurrentAccountId,
    string? LightstreamerEndpoint,
    DateTimeOffset? SessionExpiresAtUtc,
    IReadOnlyDictionary<string, string> ResponseHeaders,
    string RawNonSecretPayloadJson);
