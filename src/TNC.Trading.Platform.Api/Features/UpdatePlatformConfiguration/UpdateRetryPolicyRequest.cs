namespace TNC.Trading.Platform.Api.Features.UpdatePlatformConfiguration;

internal sealed record UpdateRetryPolicyRequest(
    int InitialDelaySeconds,
    int MaxAutomaticRetries,
    int Multiplier,
    int MaxDelaySeconds,
    int PeriodicDelayMinutes);
