using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Features.AccountPreferences;

internal sealed record NudgeAccountPreferencesVerificationRequest(
    PlatformEnvironmentKind PlatformEnvironment,
    BrokerEnvironmentKind BrokerEnvironment,
    string AccountId,
    string AuthenticationSnapshotId,
    DateTimeOffset AuthenticatedAtUtc);