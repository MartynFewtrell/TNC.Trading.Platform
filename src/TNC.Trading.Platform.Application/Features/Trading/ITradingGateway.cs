namespace TNC.Trading.Platform.Application.Features.Trading;

internal interface ITradingGateway
{
    Task<TradingGatewayOutcome<IReadOnlyList<OpenPosition>>> ListOpenPositionsAsync(
        string expectedAccountId,
        CancellationToken cancellationToken);

    Task<TradingGatewayOutcome<OpenPosition>> GetOpenPositionAsync(
        string expectedAccountId,
        string dealId,
        CancellationToken cancellationToken);

    Task<TradingGatewayOutcome<IReadOnlyList<WorkingOrder>>> ListWorkingOrdersAsync(
        string expectedAccountId,
        CancellationToken cancellationToken);

    Task<TradingGatewayOutcome<DealConfirmation>> GetDealConfirmationAsync(
        string expectedAccountId,
        string dealReference,
        CancellationToken cancellationToken);

    Task<TradingGatewayOutcome<TradingMutationAcknowledgement>> CreatePositionAsync(
        string expectedAccountId,
        PositionCreateRequest request,
        CancellationToken cancellationToken);

    Task<TradingGatewayOutcome<TradingMutationAcknowledgement>> AmendPositionAsync(
        string expectedAccountId,
        string dealId,
        PositionAmendmentRequest request,
        CancellationToken cancellationToken);

    Task<TradingGatewayOutcome<TradingMutationAcknowledgement>> ClosePositionAsync(
        string expectedAccountId,
        PositionCloseRequest request,
        CancellationToken cancellationToken);

    Task<TradingGatewayOutcome<TradingMutationAcknowledgement>> CreateWorkingOrderAsync(
        string expectedAccountId,
        WorkingOrderCreateRequest request,
        CancellationToken cancellationToken);

    Task<TradingGatewayOutcome<TradingMutationAcknowledgement>> AmendWorkingOrderAsync(
        string expectedAccountId,
        string dealId,
        WorkingOrderAmendmentRequest request,
        CancellationToken cancellationToken);

    Task<TradingGatewayOutcome<TradingMutationAcknowledgement>> CancelWorkingOrderAsync(
        string expectedAccountId,
        string dealId,
        CancellationToken cancellationToken);
}
