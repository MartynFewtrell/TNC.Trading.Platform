using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Api.Infrastructure.Platform;

namespace TNC.Trading.Platform.Api.Features.UpdatePlatformConfiguration;

internal sealed class UpdatePlatformConfigurationValidator
{
    public void Validate(UpdatePlatformConfigurationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new Dictionary<string, string[]>();

        if (!Enum.TryParse<BrokerEnvironmentKind>(request.BrokerEnvironment, ignoreCase: true, out var brokerEnvironment))
        {
            errors[nameof(request.BrokerEnvironment)] = ["Broker environment must be Demo or Live."];
        }

        if (!Enum.TryParse<WeekendBehavior>(request.TradingSchedule.WeekendBehavior, ignoreCase: true, out _))
        {
            errors[$"{nameof(request.TradingSchedule)}.{nameof(request.TradingSchedule.WeekendBehavior)}"] = ["Weekend behavior is invalid."];
        }

        if (request.InstrumentUpdatesPerDay is < 0)
        {
            errors[nameof(request.InstrumentUpdatesPerDay)] = ["Instrument updates per day cannot be negative."];
        }

        if (request.ApprovedNonTradingDailyRequestAllowance is < 0)
        {
            errors[nameof(request.ApprovedNonTradingDailyRequestAllowance)] = ["The approved daily request allowance cannot be negative."];
        }

        if (request.MarketDataLeadInMinutes is < 0)
        {
            errors[nameof(request.MarketDataLeadInMinutes)] = ["Market-data lead-in cannot be negative."];
        }

        if (errors.Count > 0)
        {
            throw new PlatformValidationException(errors);
        }
    }
}
