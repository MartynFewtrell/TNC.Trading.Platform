using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Features.GetIgLoginHistory;

internal sealed record GetIgLoginHistoryResponse(IReadOnlyList<IgLoginSnapshot> RetainedSnapshots);
