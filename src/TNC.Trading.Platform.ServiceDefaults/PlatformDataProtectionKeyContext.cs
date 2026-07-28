using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// EF Core context for the shared ASP.NET Core Data Protection key ring.
/// </summary>
public sealed class PlatformDataProtectionKeyContext(DbContextOptions<PlatformDataProtectionKeyContext> options)
    : DbContext(options), IDataProtectionKeyContext
{
    /// <summary>
    /// Gets the persisted Data Protection keys.
    /// </summary>
    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;
}