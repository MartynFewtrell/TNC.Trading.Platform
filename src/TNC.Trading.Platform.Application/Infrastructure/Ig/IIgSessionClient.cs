namespace TNC.Trading.Platform.Application.Infrastructure.Ig;

internal interface IIgSessionClient
{
    Task<IgAuthenticateResponse> AuthenticateAsync(
        IgAuthenticateRequest request,
        CancellationToken cancellationToken);

    Task<IgAccountsResponse> GetAccountsAsync(
        string cst,
        string securityToken,
        string apiKey,
        CancellationToken cancellationToken);

    Task<IgPositionsResponse> GetPositionsAsync(
        string cst,
        string securityToken,
        string apiKey,
        CancellationToken cancellationToken);
}
