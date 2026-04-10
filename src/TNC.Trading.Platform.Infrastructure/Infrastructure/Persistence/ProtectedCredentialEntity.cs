namespace TNC.Trading.Platform.Infrastructure.Persistence;

internal sealed class ProtectedCredentialEntity
{
    public int CredentialId { get; set; }

    public string BrokerEnvironment { get; set; } = string.Empty;

    public string CredentialType { get; set; } = string.Empty;

    public string ProtectedValue { get; set; } = string.Empty;

    public string ProtectionKind { get; set; } = "DataProtection";

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public string UpdatedBy { get; set; } = string.Empty;
}
