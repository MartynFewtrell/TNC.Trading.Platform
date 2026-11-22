namespace TNC.Trading.Platform.Infrastructure.UnitTests.Integrations.Ig;

internal sealed class ImmediateTimeProvider : TimeProvider
{
    public override ITimer CreateTimer(
        TimerCallback callback,
        object? state,
        TimeSpan dueTime,
        TimeSpan period)
    {
        var timer = new ImmediateTimer(callback, state);
        timer.Change(dueTime, period);
        return timer;
    }

    private sealed class ImmediateTimer(
        TimerCallback callback,
        object? state) : ITimer
    {
        private bool disposed;

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            if (disposed)
            {
                return false;
            }

            if (dueTime != Timeout.InfiniteTimeSpan)
            {
                callback(state);
            }

            return true;
        }

        public void Dispose() => disposed = true;

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
