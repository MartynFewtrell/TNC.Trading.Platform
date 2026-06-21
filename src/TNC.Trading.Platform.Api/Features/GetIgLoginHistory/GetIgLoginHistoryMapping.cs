using AppGetIgLoginHistory = TNC.Trading.Platform.Application.Features.GetIgLoginHistory;

namespace TNC.Trading.Platform.Api.Features.GetIgLoginHistory;

internal static class GetIgLoginHistoryMapping
{
    public static GetIgLoginHistoryResponse ToResponse(this AppGetIgLoginHistory.GetIgLoginHistoryResponse response) =>
        new(response.RetainedSnapshots
            .Select(s => new IgLoginHistorySnapshotResponse(
                s.Id,
                s.CapturedAtUtc,
                s.TradingDay,
                s.CurrentAccountId,
                s.LightstreamerEndpoint,
                s.SessionExpiresAtUtc,
                s.ResponseHeaders,
                s.RawNonSecretPayloadJson))
            .ToList());
}
