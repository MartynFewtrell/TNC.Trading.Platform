namespace TNC.Trading.Platform.Application.Features.AccountDetails;

internal sealed record AccountDetailsCursor(DateTimeOffset RetrievedAtUtc, Guid RetrievalId)
{
    public string Encode() => $"{RetrievedAtUtc.UtcTicks}:{RetrievalId:N}";

    public static bool TryDecode(string? value, out AccountDetailsCursor? cursor)
    {
        cursor = null;
        var parts = value?.Split(':', 2);
        if (parts is not [var ticks, var id]
            || !long.TryParse(ticks, out var parsedTicks)
            || !Guid.TryParseExact(id, "N", out var parsedId))
        {
            return false;
        }

        cursor = new AccountDetailsCursor(new DateTimeOffset(parsedTicks, TimeSpan.Zero), parsedId);
        return true;
    }
}
