namespace TNC.Trading.Platform.Application.Infrastructure.Ig;

internal sealed record IgPositionsResponse(
    IReadOnlyList<IgPositionItem> Positions);
