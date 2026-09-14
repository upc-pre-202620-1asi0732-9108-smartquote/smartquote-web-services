using SmartQuote.API.QuotationIntake.Application.OutboundServices;

namespace SmartQuote.API.EvaluationSimulation.Application.Ports;

public interface IVerifiedQuotationSnapshotReader
{
    Task<IReadOnlyList<VerifiedQuotationSnapshot>> GetCurrentForRequestAsync(string requestId, CancellationToken cancellationToken = default);
}
