namespace TNC.Trading.Platform.Api.Features.GetPlatformStatus;

internal sealed record TradingScheduleStateResponse(
    bool IsActive,
    string Reason);
