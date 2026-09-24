namespace TNC.Trading.Platform.Api.Features.Platform;

internal sealed record InstrumentPageCursor(
    string BrokerEnvironment,
    string CategoryCode,
    long SnapshotVersion,
    string AfterEpic);
