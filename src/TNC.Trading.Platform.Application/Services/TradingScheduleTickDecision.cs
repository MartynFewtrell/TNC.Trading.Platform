namespace TNC.Trading.Platform.Application.Services;

internal sealed record TradingScheduleTickDecision(TradingScheduleTickAction Action, string? Reason = null)
{
    public static TradingScheduleTickDecision Allowed() =>
        new(TradingScheduleTickAction.Allowed);

    public static TradingScheduleTickDecision BlockedBySchedule(string reason) =>
        new(TradingScheduleTickAction.BlockedBySchedule, reason);

    public static TradingScheduleTickDecision BlockedLive() =>
        new(TradingScheduleTickAction.BlockedLive);
}