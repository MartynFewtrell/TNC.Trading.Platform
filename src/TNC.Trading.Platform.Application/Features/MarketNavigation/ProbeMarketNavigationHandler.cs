using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

namespace TNC.Trading.Platform.Application.Features.MarketNavigation;

internal sealed class ProbeMarketNavigationHandler(
    IMarketNavigationGateway gateway,
    IMarketCategoryInstrumentRequestBudget requestBudget)
{
    internal async Task<ProbeMarketNavigationResponse> HandleAsync(
        ProbeMarketNavigationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!ProbeMarketNavigationValidator.IsValid(request))
        {
            return new("Refused", 0, null, null);
        }

        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(request.MaximumDurationSeconds));
        using var execution = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, deadline.Token, request.BudgetContext.ScheduleCancellationToken);
        var scope = new MarketNavigationProbeScope(request);
        MarketNavigationShape? roots = null;
        try
        {
            if (!await requestBudget.IsExecutionContextStillActiveAsync(BrokerEnvironmentKind.Demo, request.BudgetContext, execution.Token).ConfigureAwait(false))
            {
                return new("ExecutionInvalidated", 0, null, null);
            }

            var remaining = await requestBudget.GetRemainingManualRequestBudgetAsync(BrokerEnvironmentKind.Demo, request.BudgetContext, execution.Token).ConfigureAwait(false);
            if (remaining is null || remaining < request.MaximumHttpAttempts)
            {
                return new("AllowanceInsufficient", 0, null, null);
            }

            var rootResult = await gateway.GetAsync(null, scope, execution.Token).ConfigureAwait(false);
            roots = rootResult.Shape;
            if (rootResult.Outcome != "Observed" || roots is null)
            {
                return new(rootResult.Outcome, scope.HttpAttempts, roots, null);
            }

            var nodeId = request.NodeId ?? roots.NodeIds.Order(StringComparer.Ordinal).FirstOrDefault();
            if (nodeId is null || !roots.NodeIds.Contains(nodeId, StringComparer.Ordinal))
            {
                return new("SelectedRootUnavailable", scope.HttpAttempts, roots, null);
            }

            var child = await gateway.GetAsync(nodeId, scope, execution.Token).ConfigureAwait(false);
            return new(child.Outcome, scope.HttpAttempts, roots, child.Shape);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(deadline.IsCancellationRequested ? "CaptureDurationExceeded" : "ExecutionInvalidated", scope.HttpAttempts, roots, null);
        }
    }
}