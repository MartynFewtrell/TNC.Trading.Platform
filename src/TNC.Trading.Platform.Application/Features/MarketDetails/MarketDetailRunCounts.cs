namespace TNC.Trading.Platform.Application.Features.MarketDetails;

internal sealed record MarketDetailRunCounts
{
    public MarketDetailRunCounts(int expectedCount, int completedCount, int excludedCount)
    {
        if (expectedCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedCount));
        }

        if (completedCount < 0 || completedCount > expectedCount)
        {
            throw new ArgumentOutOfRangeException(nameof(completedCount));
        }

        if (excludedCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(excludedCount));
        }

        ExpectedCount = expectedCount;
        CompletedCount = completedCount;
        ExcludedCount = excludedCount;
    }

    public int ExpectedCount { get; }

    public int CompletedCount { get; }

    public int ExcludedCount { get; }

    public int FrozenCount => checked(ExpectedCount + ExcludedCount);

    public int OutstandingCount => ExpectedCount - CompletedCount;
}
