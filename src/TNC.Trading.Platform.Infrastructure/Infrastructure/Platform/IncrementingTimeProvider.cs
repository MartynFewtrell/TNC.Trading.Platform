namespace TNC.Trading.Platform.Infrastructure.Platform;

internal sealed class IncrementingTimeProvider(DateTimeOffset initialUtcNow, TimeSpan step) : TimeProvider
{
    private readonly Lock syncLock = new();
    private DateTimeOffset currentUtcNow = initialUtcNow;

    public override DateTimeOffset GetUtcNow()
    {
        lock (syncLock)
        {
            var value = currentUtcNow;
            currentUtcNow = currentUtcNow.Add(step);
            return value;
        }
    }
}
