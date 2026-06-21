namespace TNC.Trading.Platform.Api.Features.GetIgLoginHistory;

internal sealed record GetIgLoginHistoryResponse(IReadOnlyList<IgLoginHistorySnapshotResponse> RetainedSnapshots);
