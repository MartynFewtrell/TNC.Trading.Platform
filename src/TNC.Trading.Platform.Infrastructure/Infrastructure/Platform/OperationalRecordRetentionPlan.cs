namespace TNC.Trading.Platform.Infrastructure.Platform;

internal sealed record OperationalRecordRetentionPlan(
    DateTimeOffset Cutoff,
    string RetainedSnapshotKind);