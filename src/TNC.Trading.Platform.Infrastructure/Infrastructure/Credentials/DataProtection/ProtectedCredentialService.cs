using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

namespace TNC.Trading.Platform.Infrastructure.Credentials.DataProtection;

internal sealed class ProtectedCredentialService(
    PlatformDbContext dbContext,
    IDataProtectionProvider dataProtectionProvider,
    TimeProvider timeProvider) : IProtectedCredentialService
{
    private readonly IDataProtector protector = dataProtectionProvider.CreateProtector("Platform.IgCredentials");

    public async Task<CredentialPresence> GetPresenceAsync(BrokerEnvironmentKind brokerEnvironment, CancellationToken cancellationToken)
    {
        var entities = await dbContext.ProtectedCredentials
            .Where(item => item.BrokerEnvironment == brokerEnvironment.ToString())
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        bool IsUsable(string type)
        {
            var entity = entities.FirstOrDefault(item => string.Equals(item.CredentialType, type, StringComparison.Ordinal));
            if (entity is null)
            {
                return false;
            }

            try
            {
                return !string.IsNullOrWhiteSpace(protector.Unprotect(entity.ProtectedValue));
            }
            catch (CryptographicException)
            {
                return false;
            }
        }

        var credentialTypes = entities
            .Select(item => item.CredentialType)
            .ToArray();

        return new CredentialPresence(
            credentialTypes.Contains("ApiKey", StringComparer.Ordinal),
            credentialTypes.Contains("Identifier", StringComparer.Ordinal),
            credentialTypes.Contains("Password", StringComparer.Ordinal),
            IsUsable("ApiKey"),
            IsUsable("Identifier"),
            IsUsable("Password"));
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

    public async Task<IgCredentials> GetCredentialsAsync(BrokerEnvironmentKind brokerEnvironment, CancellationToken cancellationToken)
    {
        var entities = await dbContext.ProtectedCredentials
            .Where(item => item.BrokerEnvironment == brokerEnvironment.ToString())
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        string? Decrypt(string type)
        {
            var entity = entities.FirstOrDefault(item => string.Equals(item.CredentialType, type, StringComparison.Ordinal));
            if (entity is null)
            {
                return null;
            }

            try
            {
                return protector.Unprotect(entity.ProtectedValue);
            }
            catch (CryptographicException)
            {
                return null;
            }
        }

        return new IgCredentials(
            Decrypt("ApiKey") ?? string.Empty,
            Decrypt("Identifier") ?? string.Empty,
            Decrypt("Password") ?? string.Empty);
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
