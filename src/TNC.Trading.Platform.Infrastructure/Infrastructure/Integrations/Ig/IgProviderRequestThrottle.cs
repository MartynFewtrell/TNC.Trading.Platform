namespace TNC.Trading.Platform.Infrastructure.Integrations.Ig;

internal sealed class IgProviderRequestThrottle(TimeSpan? minimumInterval = null)
{
    private readonly object sync = new();
    private readonly TimeSpan interval = minimumInterval ?? TimeSpan.FromMilliseconds(250);
    private DateTimeOffset nextRequestAtUtc;

    internal async Task WaitAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset reservedAtUtc;
        lock (sync)
        {
            var now = DateTimeOffset.UtcNow;
            reservedAtUtc = nextRequestAtUtc > now ? nextRequestAtUtc : now;
            nextRequestAtUtc = reservedAtUtc.Add(interval);
        }

        var delay = reservedAtUtc - DateTimeOffset.UtcNow;
        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
    }
}
