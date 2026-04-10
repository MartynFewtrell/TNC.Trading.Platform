namespace TNC.Trading.Platform.Api.Features.GetPlatformConfiguration;

internal sealed record ConfigurationRetryPolicyResponse(
    int InitialDelaySeconds,
    int MaxAutomaticRetries,
    int Multiplier,
    int MaxDelaySeconds,
    int PeriodicDelayMinutes);
