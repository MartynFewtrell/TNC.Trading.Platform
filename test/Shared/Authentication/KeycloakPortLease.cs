using System.Diagnostics;

namespace TNC.Trading.Platform.TestShared.Authentication;

internal sealed class KeycloakPortLease : IAsyncDisposable
{
    public static readonly TimeSpan DefaultAcquisitionTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);
    private static readonly string LeaseDirectory = Path.Combine(Path.GetTempPath(), "TNC.Trading.Platform", "leases");

    private readonly FileStream lockStream;
    private bool disposed;

    private KeycloakPortLease(FileStream lockStream, string lockPath)
    {
        this.lockStream = lockStream;
        LockPath = lockPath;
    }

    public string LockPath { get; }

    public static Task<KeycloakPortLease> AcquireAsync(CancellationToken cancellationToken = default) =>
        AcquireAsync(DefaultAcquisitionTimeout, cancellationToken);

    public static async Task<KeycloakPortLease> AcquireAsync(TimeSpan acquisitionTimeout, CancellationToken cancellationToken = default)
    {
        if (acquisitionTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(acquisitionTimeout), "The acquisition timeout must be positive.");
        }

        Directory.CreateDirectory(LeaseDirectory);
        var lockPath = Path.Combine(LeaseDirectory, "keycloak-port-8080.lock");
        var stopwatch = Stopwatch.StartNew();

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var stream = new FileStream(lockPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
                await WriteOwnerMetadataAsync(stream, cancellationToken).ConfigureAwait(false);
                return new KeycloakPortLease(stream, lockPath);
            }
            catch (IOException)
            {
                TryRemoveStaleLockFile(lockPath);

                if (stopwatch.Elapsed >= acquisitionTimeout)
                {
                    throw new TimeoutException(BuildDiagnostic("timed out", lockPath, stopwatch.Elapsed));
                }

                var remaining = acquisitionTimeout - stopwatch.Elapsed;
                await Task.Delay(remaining < PollInterval ? remaining : PollInterval, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static void TryRemoveStaleLockFile(string lockPath)
    {
        try
        {
            using var stream = new FileStream(lockPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            stream.Dispose();
            File.Delete(lockPath);
        }
        catch (FileNotFoundException)
        {
        }
        catch (IOException)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        await lockStream.DisposeAsync().ConfigureAwait(false);
        for (var attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                File.Delete(LockPath);
                return;
            }
            catch (FileNotFoundException)
            {
                return;
            }
            catch (IOException) when (attempt < 9)
            {
                await Task.Delay(PollInterval).ConfigureAwait(false);
            }
        }
    }

    private static async Task WriteOwnerMetadataAsync(FileStream stream, CancellationToken cancellationToken)
    {
        var process = Process.GetCurrentProcess();
        var metadata = $"pid={process.Id};process={process.ProcessName};started={DateTimeOffset.UtcNow:O}{Environment.NewLine}";
        await using var writer = new StreamWriter(stream, leaveOpen: true);
        await writer.WriteAsync(metadata.AsMemory(), cancellationToken).ConfigureAwait(false);
        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string BuildDiagnostic(string reason, string lockPath, TimeSpan elapsed) =>
        $"Keycloak port 8080 lease {reason}. LockPath='{lockPath}', Elapsed='{elapsed}', Process='{Process.GetCurrentProcess().ProcessName}', PID='{Environment.ProcessId}'.";
}