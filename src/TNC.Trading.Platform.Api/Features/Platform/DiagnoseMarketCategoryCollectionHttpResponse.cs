namespace TNC.Trading.Platform.Api.Features.Platform;

internal sealed record DiagnoseMarketCategoryCollectionHttpResponse(
    string Status,
    string? BrokerEnvironment,
    string CategoryCode,
    int MaximumHttpRequests,
    int? AvailableHttpRequests,
    string? SafeReasonCode,
    MarketCategoryInstrumentDiagnosticResponse? Diagnostic);
