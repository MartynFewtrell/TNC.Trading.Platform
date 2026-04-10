namespace TNC.Trading.Platform.Application.Configuration;

internal sealed record RetryPolicyConfiguration(
    int InitialDelaySeconds,
    int MaxAutomaticRetries,
    int Multiplier,
    int MaxDelaySeconds,
    int PeriodicDelayMinutes);
