namespace SmartQuote.Modules.EvaluationSimulation.Domain.Model.ValueObjects;

public record DistributedCandidate(string ItemId, string QuotationId, string? LineId, bool IsEligible, decimal Score, IReadOnlyList<string> Exclusions);
public record DistributedAllocation(string RequestedItemId, string QuotationId, string QuotationLineId,
    Guid SupplierAccountId, long SupplierEligibilityVersion, string SupplierBusinessName, string SupplierTaxIdentifier,
    string Description, decimal Quantity, string UnitOfMeasure, string OriginalCurrency, decimal OriginalUnitPrice,
    decimal OriginalTotal, decimal UnitPricePen, decimal AmountPen, decimal Score, int DeliveryLeadTimeDays);
public record DistributedSimulationResult(bool CanApprove, IReadOnlyList<string> UncoveredItemIds,
    IReadOnlyList<DistributedAllocation> Allocations, IReadOnlyList<DistributedCandidate> Candidates, decimal TotalPen);
