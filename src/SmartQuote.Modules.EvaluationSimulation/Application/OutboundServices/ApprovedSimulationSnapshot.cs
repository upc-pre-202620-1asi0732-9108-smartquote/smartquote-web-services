namespace SmartQuote.Modules.EvaluationSimulation.Application.OutboundServices;

public record ApprovedSimulationSnapshot(
    string SimulationRunId,
    string RequestId,
    string SelectedQuotationId,
    string SupplierId,
    string SupplierName,
    string SupplierTaxIdentifier,
    string Currency,
    IReadOnlyList<ApprovedOrderLineSnapshot> OrderLines,
    int DeliveryLeadTimeDays,
    string InputFingerprint,
    bool IsCurrent);

public record ApprovedOrderLineSnapshot(
    string? SourceQuotationLineId,
    string? SourceRequestedItemId,
    string Description,
    decimal Quantity,
    string UnitOfMeasure,
    decimal UnitPrice);
