namespace TNC.Trading.Platform.Application.Services;

internal static class TrailingStopsPreferenceObservationRetentionPolicy
{
    public const int RetentionDays = 90;

    public static DateTimeOffset GetCutoff(DateTimeOffset utcNow) => utcNow.AddDays(-RetentionDays);
}