namespace SmartQuote.Modules.QuotationIntake.Application.OutboundServices;

/// <summary>
/// Published contract: the only way other bounded contexts may read verified QuotationIntake state.
/// </summary>
public interface IVerifiedQuotationSnapshotProvider
{
    Task<IReadOnlyList<VerifiedQuotationSnapshot>> GetVerifiedForRequestAsync(string requestId, CancellationToken cancellationToken = default);
}

