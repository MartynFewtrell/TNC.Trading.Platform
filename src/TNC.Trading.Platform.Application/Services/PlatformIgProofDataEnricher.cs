using Microsoft.Extensions.Logging;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Infrastructure.Ig;

namespace TNC.Trading.Platform.Application.Services;

internal sealed class PlatformIgProofDataEnricher(
    IIgSessionClient igSessionClient,
    IPlatformIgProofDataStore igProofDataStore,
    TimeProvider timeProvider,
    ILogger<PlatformStateCoordinator> logger)
{
    public async Task TryCaptureAsync(
        PlatformConfigurationSnapshot currentConfiguration,
        IgAuthenticateResponse authResponse,
        CancellationToken cancellationToken)
    {
        var cst = authResponse.ClientSessionToken;
        var securityToken = authResponse.AccountSecurityToken;
        var apiKey = authResponse.Headers.GetValueOrDefault("X-IG-API-KEY") ?? string.Empty;

        if (string.IsNullOrEmpty(cst) || string.IsNullOrEmpty(securityToken))
        {
            logger.LogWarning("Proof-data query skipped: session tokens not present in auth response.");
            return;
        }

        try
        {
            var accountsResponse = await igSessionClient
                .GetAccountsAsync(cst, securityToken, apiKey, cancellationToken)
                .ConfigureAwait(false);

            var positionsResponse = await igSessionClient
                .GetPositionsAsync(cst, securityToken, apiKey, cancellationToken)
                .ConfigureAwait(false);

            var preferred = accountsResponse.Accounts.FirstOrDefault(a => a.Preferred)
                ?? accountsResponse.Accounts.FirstOrDefault();

            var snapshot = new IgProofDataSnapshot(
                preferred?.AccountName,
                preferred?.AccountId,
                preferred?.Balance?.Balance,
                positionsResponse.Positions.Count,
                timeProvider.GetUtcNow());

            await igProofDataStore
                .SaveAsync(currentConfiguration.BrokerEnvironment, snapshot, cancellationToken)
                .ConfigureAwait(false);

            logger.LogInformation(
                "IG proof data captured: account={AccountName}, balance={Balance}, positions={PositionCount}",
                preferred?.AccountName ?? "none",
                preferred?.Balance?.Balance,
                positionsResponse.Positions.Count);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "IG proof-data query failed; session remains active.");
        }
    }
}