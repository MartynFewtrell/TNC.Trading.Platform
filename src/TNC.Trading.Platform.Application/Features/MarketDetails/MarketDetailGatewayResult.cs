namespace TNC.Trading.Platform.Application.Features.MarketDetails;

internal sealed record MarketDetailGatewayResult
{
    private MarketDetailGatewayResult(
        string epic,
        MarketDetailValidatedObservation? observation,
        MarketDetailGatewayFailure? failure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(epic);
        if ((observation is null) == (failure is null))
        {
            throw new ArgumentException("A gateway result must contain exactly one observation or failure.");
        }

        if (observation is not null && !string.Equals(epic, observation.Epic, StringComparison.Ordinal))
        {
            throw new ArgumentException("The result EPIC must match the validated observation.", nameof(observation));
        }

        Epic = epic;
        Observation = observation;
        Failure = failure;
    }

    public string Epic { get; }

    public MarketDetailValidatedObservation? Observation { get; }

    public MarketDetailGatewayFailure? Failure { get; }

    public static MarketDetailGatewayResult Succeeded(MarketDetailValidatedObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        return new(observation.Epic, observation, null);
    }

    public static MarketDetailGatewayResult Failed(string epic, MarketDetailGatewayFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new(epic, null, failure);
    }
}
