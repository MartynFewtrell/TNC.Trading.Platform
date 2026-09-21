using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Features.AccountDetails;

internal sealed record AccountDetailsSnapshot(
    Guid RetrievalId,
    BrokerEnvironmentKind BrokerEnvironment,
    DateTimeOffset RetrievedAtUtc,
    DateOnly TradingDay,
    AccountDetailsTriggerSource TriggerSource,
    IReadOnlyList<AccountDetailsAccount> Accounts);
