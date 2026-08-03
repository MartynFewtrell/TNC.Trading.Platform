namespace TNC.Trading.Platform.Application.Features.PlatformAuthentication.Ports;

internal sealed record BrokerAuthenticationOutcome(
    BrokerAuthenticationEvidence? Evidence,
    BrokerAuthenticationProof? Proof,
    BrokerAuthenticationFailure? Failure)
{
    public bool IsAuthenticated => Evidence is not null && Failure is null;

    public static BrokerAuthenticationOutcome Succeeded(
        BrokerAuthenticationEvidence evidence,
        BrokerAuthenticationProof? proof) =>
        new(evidence, proof, null);

    public static BrokerAuthenticationOutcome Failed(BrokerAuthenticationFailure failure) =>
        new(null, null, failure);
}