namespace TNC.Trading.Platform.Application.Features.BrokerEnvironments;

public interface IBrokerEnvironmentCatalogService
{
    Task<IReadOnlyList<BrokerEnvironmentCatalogItem>> ListAsync(CancellationToken cancellationToken);
    Task<BrokerEnvironmentOperationResult> CreateAsync(CreateBrokerEnvironmentCommand command, CancellationToken cancellationToken);
    Task<BrokerEnvironmentOperationResult> SaveCredentialsAsync(SaveBrokerEnvironmentCredentialsCommand command, CancellationToken cancellationToken);
    Task<BrokerEnvironmentOperationResult> SelectAsync(SelectBrokerEnvironmentCommand command, CancellationToken cancellationToken);
    Task<BrokerEnvironmentStatus> GetStatusAsync(CancellationToken cancellationToken);
    Task<BrokerEnvironmentRetirementPreview?> PreviewRetirementAsync(Guid brokerEnvironmentId, string actor, CancellationToken cancellationToken);
    Task<BrokerEnvironmentRetirementResult> RetireAsync(RetireBrokerEnvironmentCommand command, CancellationToken cancellationToken);
}