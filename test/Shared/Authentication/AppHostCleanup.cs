namespace TNC.Trading.Platform.TestShared.Authentication;

internal static class AppHostCleanup
{
    public static async Task DisposeAsync(
        Func<Task> stopApplication,
        Func<ValueTask> disposeApplication,
        Func<ValueTask> disposeBuilder)
    {
        Exception? firstException = null;
        List<Exception>? cleanupExceptions = null;

        try { await stopApplication().ConfigureAwait(false); }
        catch (Exception exception) { firstException ??= exception; (cleanupExceptions ??= []).Add(exception); }

        try { await disposeApplication().ConfigureAwait(false); }
        catch (Exception exception) { firstException ??= exception; (cleanupExceptions ??= []).Add(exception); }

        try { await disposeBuilder().ConfigureAwait(false); }
        catch (Exception exception) { firstException ??= exception; (cleanupExceptions ??= []).Add(exception); }

        if (firstException is not null)
        {
            if (cleanupExceptions is { Count: > 1 })
            {
                firstException.Data["CleanupExceptions"] = cleanupExceptions.Skip(1).ToArray();
            }

            throw firstException;
        }
    }
}