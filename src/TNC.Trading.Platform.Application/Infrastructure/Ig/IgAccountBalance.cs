namespace TNC.Trading.Platform.Application.Infrastructure.Ig;

internal sealed record IgAccountBalance(
    decimal Balance,
    decimal Deposit,
    decimal ProfitLoss,
    decimal Available);
