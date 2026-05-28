using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace TNC.Trading.Platform.Web.UnitTests;

internal sealed class TestWebHostEnvironment : IWebHostEnvironment
{
    public string ApplicationName { get; set; } = "TNC.Trading.Platform.Web.UnitTests";

    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();

    public string WebRootPath { get; set; } = string.Empty;

    public string EnvironmentName { get; set; } = Environments.Development;

    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
