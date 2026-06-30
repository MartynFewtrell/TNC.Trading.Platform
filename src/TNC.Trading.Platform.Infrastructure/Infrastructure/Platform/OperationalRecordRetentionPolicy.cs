using Microsoft.Extensions.Configuration;
using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Infrastructure.Platform;

internal static class OperationalRecordRetentionPolicy
{
    public static int GetRetentionDays(IConfiguration configuration)
    {
        var configuredValue = configuration["Retention:OperationalRecordsDays"];
        return int.TryParse(configuredValue, out var retentionDays) && retentionDays > 0
            ? retentionDays
            : 90;
    }

    public static OperationalRecordRetentionPlan CreatePlan(DateTimeOffset cutoff)
        => new(cutoff, IgLoginSnapshotKind.RetainedDailyFirstSuccessful.ToString());
}