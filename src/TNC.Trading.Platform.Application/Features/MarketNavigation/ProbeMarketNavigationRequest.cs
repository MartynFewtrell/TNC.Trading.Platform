using TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

namespace TNC.Trading.Platform.Application.Features.MarketNavigation;

/// <summary>Explicitly approved, pre-admitted investigation scope. The caller owns authorization and diagnostic lease cleanup.</summary>
internal sealed record ProbeMarketNavigationRequest(
    string AuthorizationReference,
    Guid AppliedEnvironmentId,
    string EndpointProfile,
    string AccountContextHash,
    MarketCategoryInstrumentRequestBudgetContext BudgetContext,
    string? NodeId = null,
    int MaximumHttpAttempts = 10,
    int MaximumResponseBytes = 65_536,
    int MaximumDurationSeconds = 120);