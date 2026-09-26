using System.Text.Json.Serialization;

namespace TNC.Trading.Platform.Application.Features.MarketDetails;

internal sealed record MarketDetailQuantity
{
    [JsonConstructor]
    public MarketDetailQuantity(MarketDetailValuePresence presence, decimal? value, string? unit)
    {
        Presence = presence;
        Value = value;
        Unit = unit;
    }

    public MarketDetailValuePresence Presence { get; }

    public decimal? Value { get; }

    public string? Unit { get; }

    public static MarketDetailQuantity NotSupplied(string? unit = null) =>
        new(MarketDetailValuePresence.NotSupplied, null, unit);

    public static MarketDetailQuantity ExplicitNull(string? unit = null) =>
        new(MarketDetailValuePresence.ExplicitNull, null, unit);

    public static MarketDetailQuantity FromValue(decimal value, string? unit = null) =>
        new(MarketDetailValuePresence.Value, value, unit);
}
