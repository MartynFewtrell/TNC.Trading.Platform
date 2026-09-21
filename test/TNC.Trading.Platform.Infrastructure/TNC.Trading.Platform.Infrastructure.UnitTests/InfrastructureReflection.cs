using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TNC.Trading.Platform.Application.Services;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;

namespace TNC.Trading.Platform.Infrastructure.UnitTests;

internal static class InfrastructureReflection
{
    internal static PlatformDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(builder => builder.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new PlatformDbContext(options);
    }

    internal static IDataProtectionProvider CreateDataProtectionProvider() =>
        DataProtectionProvider.Create(new DirectoryInfo(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))));

    internal static ILogger<T> CreateNullLogger<T>() => NullLogger<T>.Instance;

    internal static IPlatformApplicationLogger CreateNullApplicationLogger() => new NullApplicationLogger();

    private sealed class NullApplicationLogger : IPlatformApplicationLogger
    {
        public void LogWarning(string message) { }

        public void LogWarning(Exception exception, string message) { }

        public void LogError(Exception exception, string message) { }

        public void LogInformation(string message, params object?[] arguments) { }
    }
}
