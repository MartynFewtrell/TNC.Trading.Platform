using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketDataRuns;

namespace TNC.Trading.Platform.Application.Features.MarketNavigation;

internal static class ProbeMarketNavigationValidator
{
    internal static bool IsValid(ProbeMarketNavigationRequest request) =>
        !string.IsNullOrWhiteSpace(request.AuthorizationReference)
        && request.AuthorizationReference.Length <= 128
        && !request.AuthorizationReference.Any(char.IsControl)
        && request.AppliedEnvironmentId != Guid.Empty
        && request.EndpointProfile == "IgDemo"
        && request.AccountContextHash is { Length: 64 }
        && request.AccountContextHash.All(Uri.IsHexDigit)
        && request.MaximumHttpAttempts is >= 3 and <= 10
        && request.MaximumResponseBytes is >= 256 and <= 1_048_576
        && request.MaximumDurationSeconds is >= 1 and <= 300
        && (request.NodeId is null || (request.NodeId.Length is > 0 and <= 128
            && !string.IsNullOrWhiteSpace(request.NodeId) && !request.NodeId.Any(char.IsControl)))
        && request.BudgetContext is { IsManualOperation: true, FullRunLease: { } lease } context
        && context.RequestSpacingSeconds is >= 0 and <= 60
        && lease.Trigger == MarketDataFullRunTrigger.Diagnostic
        && lease.Environment == BrokerEnvironmentKind.Demo
        && lease.AppliedBrokerEnvironmentId == request.AppliedEnvironmentId
        && lease.EndpointProfile == request.EndpointProfile
        && context.AppliedEndpointProfile == request.EndpointProfile
        && context.LeaseOwner == lease.LeaseOwner
        && context.LeaseFence == lease.LeaseFence;
}