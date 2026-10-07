using SmartQuote.Modules.EvaluationSimulation.Application.Ports;
using SmartQuote.Modules.QuotationIntake.Application.OutboundServices;

namespace SmartQuote.Modules.EvaluationSimulation.Infrastructure.ReferenceReaders;

public class VerifiedQuotationSnapshotAdapter(IVerifiedQuotationSnapshotProvider snapshotProvider) : IVerifiedQuotationSnapshotReader
{
    public Task<IReadOnlyList<VerifiedQuotationSnapshot>> GetCurrentForRequestAsync(string requestId, CancellationToken cancellationToken = default) =>
        snapshotProvider.GetVerifiedForRequestAsync(requestId, cancellationToken);
}
