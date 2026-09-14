using SmartQuote.Modules.QuotationIntake.Application.OutboundServices;

namespace SmartQuote.Modules.EvaluationSimulation.Application.Ports;

public interface IVerifiedQuotationSnapshotReader
{
    Task<IReadOnlyList<VerifiedQuotationSnapshot>> GetCurrentForRequestAsync(string requestId, CancellationToken cancellationToken = default);
}
