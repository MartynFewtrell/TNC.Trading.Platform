namespace TNC.Trading.Platform.Application.Features.TriggerManualAuthRetry;

/// <summary>Represents acceptance or an expected typed rejection of a manual retry request.</summary>
public sealed record TriggerManualAuthRetryOutcome(
    Guid? RetryCycleId,
    ManualAuthRetryRejectionReason? RejectionReason)
{
    /// <summary>Gets whether the retry cycle was accepted.</summary>
    public bool IsAccepted => RetryCycleId is not null && RejectionReason is null;

    /// <summary>Creates an accepted outcome for the started retry cycle.</summary>
    public static TriggerManualAuthRetryOutcome Accepted(Guid retryCycleId) => new(retryCycleId, null);

    /// <summary>Creates a rejected outcome without performing the retry write slice.</summary>
    public static TriggerManualAuthRetryOutcome Rejected(ManualAuthRetryRejectionReason reason) => new(null, reason);
}