namespace TNC.Trading.Platform.Api.Features.UpdatePlatformConfiguration;

internal sealed record UpdatedRetryPolicyResponse(
    int InitialDelaySeconds,
    int MaxAutomaticRetries,
    int Multiplier,
    int MaxDelaySeconds,
    int PeriodicDelayMinutes);
