namespace TNC.Trading.Platform.Application.Features.MarketDetails;

internal sealed class MarketDetailCapacityPolicy
{
    private const int MaximumEpicsPerRequest = 50;
    private const int MaximumUriLength = 1_800;
    private const int MaximumAttempts = 3;
    private const int MaximumReauthenticationReplays = 1;
    private static readonly int MaximumMarketsUriPrefixLength =
        "https://demo-api.ig.com/gateway/deal/markets?epics=".Length
        + "&filter=ALL".Length;

    public MarketDetailCapacityEstimate Estimate(IReadOnlyList<MarketDetailCapacityTarget> retryableTargets)
    {
        ArgumentNullException.ThrowIfNull(retryableTargets);
        if (retryableTargets.Any(target =>
                string.IsNullOrWhiteSpace(target.Epic)
                || target.Epic.Length > 64
                || target.Attempts is < 0 or >= MaximumAttempts)
            || retryableTargets.Select(target => target.Epic).Distinct(StringComparer.Ordinal).Count() != retryableTargets.Count)
        {
            throw new ArgumentException("Capacity targets must have unique, valid EPICs and fewer than three attempts.", nameof(retryableTargets));
        }

        var noRetryRequests = 0;
        var worstCaseRequests = 0;
        var totalMarketBatches = 0;
        var immediateBatchRequestCounts = CountMarketRequests(
            retryableTargets.Select(target => target.Epic).ToArray());
        noRetryRequests = immediateBatchRequestCounts.Count + immediateBatchRequestCounts.Sum();

        for (var attemptNumber = 0; attemptNumber < MaximumAttempts; attemptNumber++)
        {
            var epics = retryableTargets
                .Where(target => target.Attempts <= attemptNumber)
                .Select(target => target.Epic)
                .ToArray();
            var batchRequestCounts = CountMarketRequests(epics);

            totalMarketBatches += batchRequestCounts.Sum();
            worstCaseRequests += batchRequestCounts.Sum(count =>
                checked(1 + (1 + MaximumReauthenticationReplays) * count));
        }

        return new(
            noRetryRequests,
            worstCaseRequests - noRetryRequests,
            worstCaseRequests,
            totalMarketBatches);
    }

    private static IReadOnlyList<int> CountMarketRequests(IReadOnlyList<string> epics)
    {
        var operations = new List<int>();
        for (var offset = 0; offset < epics.Count; offset += MaximumEpicsPerRequest)
        {
            var operationEpics = epics.Skip(offset).Take(MaximumEpicsPerRequest).ToArray();
            var marketBatches = 0;
            var current = new List<string>(MaximumEpicsPerRequest);
            foreach (var epic in operationEpics)
            {
                var candidate = current.Append(epic).ToArray();
                if (current.Count > 0 && !FitsMarketsUri(candidate))
                {
                    marketBatches++;
                    current.Clear();
                }

                if (!FitsMarketsUri([epic]))
                {
                    throw new ArgumentException("An EPIC cannot fit within the supported bulk request URI.");
                }

                current.Add(epic);
            }

            if (current.Count > 0)
            {
                marketBatches++;
            }

            operations.Add(marketBatches);
        }

        return operations;
    }

    private static bool FitsMarketsUri(IReadOnlyList<string> epics) =>
        MaximumMarketsUriPrefixLength
        + Uri.EscapeDataString(string.Join(",", epics)).Length
        <= MaximumUriLength;
}
