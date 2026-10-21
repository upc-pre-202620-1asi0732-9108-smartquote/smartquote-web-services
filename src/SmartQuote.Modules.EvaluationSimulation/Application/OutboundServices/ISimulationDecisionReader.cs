namespace SmartQuote.Modules.EvaluationSimulation.Application.OutboundServices;

public interface ISimulationDecisionReader
{
    Task<SimulationDecisionReference> GetSourceReferenceAsync(Guid runId, string quotationId, CancellationToken cancellationToken = default);
    Task<ApprovedSimulationSnapshot?> GetApprovedSnapshotAsync(Guid runId, string quotationId, CancellationToken cancellationToken = default);
}
