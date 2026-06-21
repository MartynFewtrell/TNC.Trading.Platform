namespace TNC.Trading.Platform.Application.Infrastructure.Ig;

internal sealed record IgAccountsResponse(
    IReadOnlyList<IgAccountSummary> Accounts);
