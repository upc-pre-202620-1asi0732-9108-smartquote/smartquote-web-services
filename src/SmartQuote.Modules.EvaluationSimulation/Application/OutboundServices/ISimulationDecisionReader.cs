namespace SmartQuote.Modules.EvaluationSimulation.Application.OutboundServices;

public interface ISimulationDecisionReader
{
    Task<ApprovedSimulationSnapshot?> GetApprovedSnapshotAsync(Guid runId, string quotationId, CancellationToken cancellationToken = default);
}
