using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Services;

internal static class OperationalRecordRetentionPolicy
{
    private const int DefaultRetentionDays = 90;

    public static OperationalRecordRetentionPlan CreatePlan(
        DateTimeOffset utcNow,
        string? configuredRetentionDays)
    {
        var retentionDays = int.TryParse(configuredRetentionDays, out var configuredValue)
            && configuredValue > 0
                ? configuredValue
                : DefaultRetentionDays;

        return new OperationalRecordRetentionPlan(
            retentionDays,
            utcNow.AddDays(-retentionDays),
            IgLoginSnapshotKind.RetainedDailyFirstSuccessful);
    }
}