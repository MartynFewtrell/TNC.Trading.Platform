namespace TNC.Trading.Platform.Web;

internal sealed class UpdateRetryPolicyViewModel
{
    public int InitialDelaySeconds { get; set; }

    public int MaxAutomaticRetries { get; set; }

    public int Multiplier { get; set; }

    public int MaxDelaySeconds { get; set; }

    public int PeriodicDelayMinutes { get; set; }
}
