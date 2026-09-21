namespace TNC.Trading.Platform.Application.Features.AccountDetails;

internal sealed record GetAccountDetailsResponse(
    AccountDetailsSnapshot? Retrieval,
    string? OlderCursor,
    string? NewerCursor);
