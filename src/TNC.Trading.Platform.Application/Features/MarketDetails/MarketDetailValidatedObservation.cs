namespace TNC.Trading.Platform.Application.Features.MarketDetails;

internal sealed record MarketDetailValidatedObservation
{
    public MarketDetailValidatedObservation(
        string epic,
        DateTimeOffset retrievedAtUtc,
        string sourceEndpoint,
        int sourceVersion,
        MarketDetailObservationSource source,
        string? providerUpdateTimeText,
        MarketDetailInstrument instrument,
        MarketDetailDealingRules dealingRules,
        MarketDetailMarketSnapshot snapshot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(epic);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceEndpoint);
        ArgumentNullException.ThrowIfNull(instrument);
        ArgumentNullException.ThrowIfNull(dealingRules);
        ArgumentNullException.ThrowIfNull(snapshot);

        if (retrievedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Market detail retrieval time must be UTC.", nameof(retrievedAtUtc));
        }

        if (!string.Equals(epic, instrument.Epic, StringComparison.Ordinal))
        {
            throw new ArgumentException("The observation EPIC must match the instrument EPIC.", nameof(instrument));
        }

        var expectedVersion = source switch
        {
            MarketDetailObservationSource.BulkV2 => 2,
            MarketDetailObservationSource.SingleV3 => 3,
            MarketDetailObservationSource.SingleV4 => 4,
            _ => throw new ArgumentOutOfRangeException(nameof(source))
        };
        if (sourceVersion != expectedVersion)
        {
            throw new ArgumentException("The endpoint source and provider version do not match.", nameof(sourceVersion));
        }

        Epic = epic;
        RetrievedAtUtc = retrievedAtUtc;
        SourceEndpoint = sourceEndpoint;
        SourceVersion = sourceVersion;
        Source = source;
        ProviderUpdateTimeText = providerUpdateTimeText;
        Instrument = instrument;
        DealingRules = dealingRules;
        Snapshot = snapshot;
    }

    public string Epic { get; }

    public DateTimeOffset RetrievedAtUtc { get; }

    public string SourceEndpoint { get; }

    public int SourceVersion { get; }

    public MarketDetailObservationSource Source { get; }

    public string? ProviderUpdateTimeText { get; }

    public MarketDetailInstrument Instrument { get; }

    public MarketDetailDealingRules DealingRules { get; }

    public MarketDetailMarketSnapshot Snapshot { get; }
}
