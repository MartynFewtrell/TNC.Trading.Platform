using Microsoft.AspNetCore.Http.HttpResults;
using TNC.Trading.Platform.Api.Features.UpdatePlatformConfiguration;
using TNC.Trading.Platform.Api.Infrastructure.Platform;
using ApplicationConfigurationValidationException = TNC.Trading.Platform.Application.Features.UpdatePlatformConfiguration.ConfigurationValidationException;
using AppUpdatePlatformConfiguration = TNC.Trading.Platform.Application.Features.UpdatePlatformConfiguration;

namespace TNC.Trading.Platform.Api.Features.Platform;

internal static class UpdatePlatformConfigurationEndpointHandler
{
    public static async Task<Results<Ok<UpdatePlatformConfigurationResponse>, ValidationProblem>> HandleAsync(
        UpdatePlatformConfigurationRequest request,
        UpdatePlatformConfigurationValidator validator,
        AppUpdatePlatformConfiguration.UpdatePlatformConfigurationHandler handler,
        CancellationToken cancellationToken)
    {
        try
        {
            validator.Validate(request);

            var result = await handler.HandleAsync(request.ToApplicationRequest(), cancellationToken);

            return TypedResults.Ok(result.ToResponse());
        }
        catch (PlatformValidationException exception)
        {
            return TypedResults.ValidationProblem(exception.Errors.ToDictionary(item => item.Key, item => item.Value));
        }
        catch (ApplicationConfigurationValidationException exception)
        {
            return TypedResults.ValidationProblem(exception.Errors.ToDictionary(item => item.Key, item => item.Value));
        }
    }
}