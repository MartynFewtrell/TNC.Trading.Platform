using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

internal sealed record DiagnoseMarketCategoryCollectionResponse(
    DiagnoseMarketCategoryCollectionStatus Status,
    BrokerEnvironmentKind? BrokerEnvironment,
    string CategoryCode,
    int MaximumHttpRequests,
    int? AvailableHttpRequests,
    string? SafeReasonCode,
    MarketCategoryInstrumentDiagnosticResult? Diagnostic);
