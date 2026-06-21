namespace TNC.Trading.Platform.Application.Infrastructure.Ig;

internal sealed record IgPositionItem(
    decimal? ContractSize,
    string? CreatedDate,
    string? Currency,
    string? DealId,
    decimal? Size,
    string? Direction);
