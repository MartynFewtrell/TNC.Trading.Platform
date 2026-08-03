namespace TNC.Trading.Platform.Application.Features.TriggerManualAuthRetry;

/// <summary>Application response for the manual authentication retry use case.</summary>
public sealed record TriggerManualAuthRetryResponse(TriggerManualAuthRetryOutcome Outcome)
{
	/// <summary>Gets the accepted retry cycle identifier.</summary>
	public Guid RetryCycleId => Outcome.RetryCycleId!.Value;
}
