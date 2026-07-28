using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;

internal sealed class PlatformDbContextFactory : IDesignTimeDbContextFactory<PlatformDbContext>
{
    public PlatformDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=TncTradingPlatform.Migrations;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        return new PlatformDbContext(options);
    }
}