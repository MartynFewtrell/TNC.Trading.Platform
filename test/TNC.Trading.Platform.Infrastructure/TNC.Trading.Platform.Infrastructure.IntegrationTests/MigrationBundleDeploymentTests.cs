using System.Diagnostics;
using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Infrastructure.Startup;

namespace TNC.Trading.Platform.Infrastructure.IntegrationTests;

[Collection("SQL Server")]
public sealed class MigrationBundleDeploymentTests(SqlServerDatabaseFixture fixture)
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    /// <summary>
    /// Trace: Environment Model Rationalisation Phase 6.1.
    /// Verifies: the selected Infrastructure bundle can create and then upgrade the owned fixture database.
    /// Expected: both executions succeed and the second execution is a no-op against the already upgraded schema.
    /// Why: deployment migrations must be a repeatable one-shot artifact rather than replica startup work.
    /// </summary>
    [Fact]
    public async Task MigrationBundle_ShouldApplyFreshAndUpgradedSchema_WhenDeploymentConnectionIsProvided()
    {
        await RunPowerShellAsync(
            "infra\\migrations\\build-migration-bundle.ps1",
            "-Configuration", "Release");

        await RunPowerShellAsync(
            "infra\\migrations\\run-migration-bundle.ps1",
            "-PlatformEnvironment", "Test",
            "-ConnectionString", fixture.ConnectionString);
        await RunPowerShellAsync(
            "infra\\migrations\\run-migration-bundle.ps1",
            "-PlatformEnvironment", "Test",
            "-ConnectionString", fixture.ConnectionString);

        await using var context = fixture.CreateDbContext();
        var appliedMigrations = await context.Database.GetAppliedMigrationsAsync(fixture.CancellationToken);

        Assert.NotEmpty(appliedMigrations);
        Assert.Equal(appliedMigrations.Count(), appliedMigrations.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// Trace: Environment Model Rationalisation Phase 6.1.
    /// Verifies: the startup ownership policy leaves Test and Live replicas non-migrating while preserving Desktop migration.
    /// Expected: only local environments return true from the shared policy.
    /// Why: a deployment identity must be the sole owner of schema changes in shared environments.
    /// </summary>
    [Theory]
    [InlineData(PlatformEnvironmentKind.Desktop, true)]
    [InlineData(PlatformEnvironmentKind.Development, true)]
    [InlineData(PlatformEnvironmentKind.Test, false)]
    [InlineData(PlatformEnvironmentKind.Live, false)]
    public void StartupMigrationPolicy_ShouldSeparateLocalAndDeploymentOwnership(
        PlatformEnvironmentKind environment,
        bool expected)
    {
        Assert.Equal(expected, PlatformStartupInitializer.ShouldApplyMigrations(environment));
    }

    private static async Task RunPowerShellAsync(params string[] arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "pwsh",
                WorkingDirectory = RepositoryRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };

        if (!IsCommandAvailable("pwsh"))
        {
            process.StartInfo.FileName = "powershell";
            process.StartInfo.ArgumentList.Add("-ExecutionPolicy");
            process.StartInfo.ArgumentList.Add("Bypass");
        }

        process.StartInfo.ArgumentList.Add("-NoProfile");
        process.StartInfo.ArgumentList.Add("-File");
        process.StartInfo.ArgumentList.Add(Path.Combine(RepositoryRoot, arguments[0]));
        foreach (var argument in arguments.Skip(1))
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        Assert.True(process.Start(), "PowerShell could not be started.");
        var standardOutput = await process.StandardOutput.ReadToEndAsync();
        var standardError = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        Assert.True(
            process.ExitCode == 0,
            $"PowerShell failed with exit code {process.ExitCode}.\nSTDOUT:\n{standardOutput}\nSTDERR:\n{standardError}");
    }

    private static bool IsCommandAvailable(string command)
    {
        using var probe = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = "-NoProfile -Command exit",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };

        try
        {
            probe.Start();
            probe.WaitForExit();
            return probe.ExitCode == 0;
        }
        catch (Win32Exception)
        {
            return false;
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "global.json")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate the repository root from the test output directory.");
    }
}
