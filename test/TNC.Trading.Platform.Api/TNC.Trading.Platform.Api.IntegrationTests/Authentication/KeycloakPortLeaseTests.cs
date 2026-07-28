using System.Diagnostics;
using TNC.Trading.Platform.TestShared.Authentication;

namespace TNC.Trading.Platform.Api.IntegrationTests.Authentication;

[Collection(RealAuthenticationIntegrationTestCollection.Name)]
public sealed class KeycloakPortLeaseTests
{
    /// <summary>
    /// Verifies the Phase 4 DD-03 cross-process contract: a real child process holding the shared file prevents another process from acquiring port 8080's lease.
    /// Expected: acquisition times out with the lock path, elapsed time, and process identity so lease contention is diagnosable separately from a bind failure.
    /// </summary>
    [Fact]
    public async Task AcquireAsync_ShouldReportBoundedTimeout_WhenChildProcessHoldsLeaseFile()
    {
        await using var child = await StartLockHoldingChildProcessAsync();

        var exception = await Assert.ThrowsAsync<TimeoutException>(() => KeycloakPortLease.AcquireAsync(TimeSpan.FromMilliseconds(500)));

        Assert.Contains("LockPath=", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Elapsed=", exception.Message, StringComparison.Ordinal);
        Assert.Contains("PID=", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies Phase 4 DD-03 release semantics: disposing the held stream removes this process's lease file.
    /// Expected: the lock file is absent after disposal, proving the stream remains held for the complete lease lifetime.
    /// </summary>
    [Fact]
    public async Task DisposeAsync_ShouldRemoveLockFile_WhenLeaseIsHeld()
    {
        string lockPath;
        await using (var lease = await KeycloakPortLease.AcquireAsync())
        {
            lockPath = lease.LockPath;
            Assert.True(File.Exists(lockPath));
        }

        Assert.False(File.Exists(lockPath));
    }

    /// <summary>
    /// Verifies Phase 4 DD-03 cancellation behavior against real file contention.
    /// Expected: cancellation interrupts bounded polling promptly rather than waiting for the full five-minute default deadline.
    /// </summary>
    [Fact]
    public async Task AcquireAsync_ShouldHonorCancellation_WhenChildProcessHoldsLeaseFile()
    {
        await using var child = await StartLockHoldingChildProcessAsync();
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => KeycloakPortLease.AcquireAsync(TimeSpan.FromSeconds(5), cancellationTokenSource.Token));
    }

    private static async Task<LockHoldingChildProcess> StartLockHoldingChildProcessAsync()
    {
        var lockPath = Path.Combine(Path.GetTempPath(), "TNC.Trading.Platform", "leases", "keycloak-port-8080.lock");
        Directory.CreateDirectory(Path.GetDirectoryName(lockPath)!);
        await RemoveStaleLockFileAsync(lockPath);

        var escapedPath = lockPath.Replace("'", "''", StringComparison.Ordinal);
        var startInfo = new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "powershell.exe" : "pwsh",
            Arguments = $"-NoProfile -NonInteractive -Command \"$stream=[System.IO.File]::Open('{escapedPath}', [System.IO.FileMode]::CreateNew, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None); [Console]::WriteLine('ready'); [Console]::Out.Flush(); [Console]::OpenStandardInput().ReadByte(); $stream.Dispose();\"",
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start the lease contention child process.");
        var ready = await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10));
        if (!string.Equals(ready, "ready", StringComparison.Ordinal))
        {
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            process.Dispose();
            throw new InvalidOperationException($"The lease contention child process did not become ready. Output: {error}");
        }

        return new LockHoldingChildProcess(process, lockPath);
    }

    private static async Task RemoveStaleLockFileAsync(string lockPath)
    {
        var deadline = Stopwatch.StartNew();

        while (true)
        {
            try
            {
                File.Delete(lockPath);
                return;
            }
            catch (FileNotFoundException)
            {
                return;
            }
            catch (IOException) when (deadline.Elapsed < TimeSpan.FromMinutes(5))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100));
            }
        }
    }

    private sealed class LockHoldingChildProcess : IAsyncDisposable
    {
        private readonly Process process;
        private readonly string lockPath;

        public LockHoldingChildProcess(Process process, string lockPath)
        {
            this.process = process;
            this.lockPath = lockPath;
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await process.StandardInput.WriteLineAsync().ConfigureAwait(false);
                await process.WaitForExitAsync().ConfigureAwait(false);
            }
            finally
            {
                process.Dispose();
            }
        }
    }
}