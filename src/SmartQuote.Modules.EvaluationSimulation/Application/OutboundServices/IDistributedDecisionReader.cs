namespace SmartQuote.Modules.EvaluationSimulation.Application.OutboundServices;

public record DistributedDecisionSnapshot(Guid RunId, string RequestId, string InputFingerprint,
    IReadOnlyList<ApprovedDistributedAllocationSnapshot> Allocations, decimal TotalPen, ApprovedExchangeRateSnapshot? ExchangeRate);
public record ApprovedDistributedAllocationSnapshot(string RequestedItemId, string QuotationId, string QuotationLineId,
    Guid SupplierAccountId, long SupplierEligibilityVersion, string SupplierBusinessName, string SupplierTaxIdentifier,
    string Description, decimal Quantity, string UnitOfMeasure, string OriginalCurrency, decimal OriginalUnitPrice,
    decimal OriginalTotal, decimal UnitPricePen, decimal AmountPen, decimal Score, int DeliveryLeadTimeDays);
public interface IDistributedDecisionReader
{
    Task<string> RequestIdAsync(Guid runId, CancellationToken ct);
    Task<DistributedDecisionSnapshot> ReadCurrentAsync(Guid runId, CancellationToken ct);
}
