namespace TNC.Trading.Platform.Application.Configuration;

internal sealed record TradingScheduleStatus(
    bool IsActive,
    string Reason);
