using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Services;

internal sealed record OperationalRecordRetentionPlan(
    int RetentionDays,
    DateTimeOffset Cutoff,
    IgLoginSnapshotKind RetainedSnapshotKind)
{
    public bool IsEligible(OperationalRecordFamily family, DateTimeOffset recordedAtUtc) =>
        family != OperationalRecordFamily.CurrentIgLoginSnapshot
        && recordedAtUtc < Cutoff;
}