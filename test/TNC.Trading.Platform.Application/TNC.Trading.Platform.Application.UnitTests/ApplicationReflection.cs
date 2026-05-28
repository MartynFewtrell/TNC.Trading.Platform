using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TNC.Trading.Platform.Infrastructure.Persistence;

namespace TNC.Trading.Platform.Application.UnitTests;

internal static class ApplicationReflection
{
    internal static PlatformDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new PlatformDbContext(options);
    }

    internal static IDataProtectionProvider CreateDataProtectionProvider() =>
        DataProtectionProvider.Create(new DirectoryInfo(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))));

    internal static ILogger<T> CreateNullLogger<T>() => NullLogger<T>.Instance;
}
