using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Features.GetPlatformConfiguration;

internal sealed record GetPlatformConfigurationResponse(PlatformConfigurationSnapshot Configuration);
