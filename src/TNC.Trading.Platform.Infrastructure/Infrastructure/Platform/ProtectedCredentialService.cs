using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Infrastructure.Persistence;

namespace TNC.Trading.Platform.Infrastructure.Platform;

internal sealed class ProtectedCredentialService(
    PlatformDbContext dbContext,
    IDataProtectionProvider dataProtectionProvider,
    TimeProvider timeProvider)
{
    private readonly IDataProtector protector = dataProtectionProvider.CreateProtector("Platform.IgCredentials");

    public async Task<CredentialPresence> GetPresenceAsync(BrokerEnvironmentKind brokerEnvironment, CancellationToken cancellationToken)
    {
        var credentialTypes = await dbContext.ProtectedCredentials
            .Where(item => item.BrokerEnvironment == brokerEnvironment.ToString())
            .Select(item => item.CredentialType)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new CredentialPresence(
            credentialTypes.Contains("ApiKey", StringComparer.Ordinal),
            credentialTypes.Contains("Identifier", StringComparer.Ordinal),
            credentialTypes.Contains("Password", StringComparer.Ordinal));
    }

    public async Task UpdateAsync(BrokerEnvironmentKind brokerEnvironment, string? apiKey, string? identifier, string? password, string changedBy, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            await UpsertCredentialAsync(brokerEnvironment, "ApiKey", apiKey, changedBy, cancellationToken).ConfigureAwait(false);
        }

        if (!string.IsNullOrWhiteSpace(identifier))
        {
            await UpsertCredentialAsync(brokerEnvironment, "Identifier", identifier, changedBy, cancellationToken).ConfigureAwait(false);
        }

        if (!string.IsNullOrWhiteSpace(password))
        {
            await UpsertCredentialAsync(brokerEnvironment, "Password", password, changedBy, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task UpsertCredentialAsync(BrokerEnvironmentKind brokerEnvironment, string credentialType, string secret, string changedBy, CancellationToken cancellationToken)
    {
        var entity = await dbContext.ProtectedCredentials
            .SingleOrDefaultAsync(
                item => item.BrokerEnvironment == brokerEnvironment.ToString() && item.CredentialType == credentialType,
                cancellationToken)
            .ConfigureAwait(false);

        entity ??= new ProtectedCredentialEntity
        {
            BrokerEnvironment = brokerEnvironment.ToString(),
            CredentialType = credentialType
        };

        entity.ProtectedValue = protector.Protect(secret);
        entity.ProtectionKind = "DataProtection";
        entity.UpdatedAtUtc = timeProvider.GetUtcNow();
        entity.UpdatedBy = changedBy;

        if (entity.CredentialId == 0)
        {
            dbContext.ProtectedCredentials.Add(entity);
        }
    }
}
