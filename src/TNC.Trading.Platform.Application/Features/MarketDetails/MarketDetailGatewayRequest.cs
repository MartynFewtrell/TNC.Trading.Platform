namespace TNC.Trading.Platform.Application.Features.MarketDetails;

internal sealed record MarketDetailGatewayRequest
{
    public MarketDetailGatewayRequest(
        IReadOnlyList<string> epics,
        MarketDetailRequestBudgetContext budgetContext)
    {
        ArgumentNullException.ThrowIfNull(epics);
        ArgumentNullException.ThrowIfNull(budgetContext);

        if (epics.Any(string.IsNullOrWhiteSpace)
            || epics.Any(epic => epic.Length > 64)
            || epics.Distinct(StringComparer.Ordinal).Count() != epics.Count)
        {
            throw new ArgumentException("A bulk request must contain unique, non-empty EPICs no longer than 64 characters.", nameof(epics));
        }

        if (epics.Count is < 1 or > 50)
        {
            throw new ArgumentOutOfRangeException(nameof(epics), "A bulk request must contain between one and fifty EPICs.");
        }

        Epics = epics.ToArray();
        BudgetContext = budgetContext;
    }

    public IReadOnlyList<string> Epics { get; }

    public MarketDetailRequestBudgetContext BudgetContext { get; }
}
