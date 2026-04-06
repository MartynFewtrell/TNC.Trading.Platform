namespace TNC.Trading.Platform.Web;

internal sealed record RetryPolicyViewModel(
    int InitialDelaySeconds,
    int MaxAutomaticRetries,
    int Multiplier,
    int MaxDelaySeconds,
    int PeriodicDelayMinutes);
