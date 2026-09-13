using SmartQuote.API.EvaluationSimulation.Application.OutboundServices;
using SmartQuote.API.PurchaseOrdering.Domain.Model.ValueObjects;

namespace SmartQuote.API.PurchaseOrdering.Application;

/// <summary>
/// Anti-corruption mapper: translates the imported ApprovedSimulationSnapshot from
/// Evaluation & Simulation into this context's own ubiquitous language.
/// </summary>
public class ApprovedDecisionMapper
{
    public ApprovedPurchaseDecision Map(ApprovedSimulationSnapshot snapshot, string deliveryConditions, string deliveryDestination) => new(
        snapshot.SimulationRunId,
        snapshot.RequestId,
        snapshot.SelectedQuotationId,
        new SupplierSnapshot(snapshot.SupplierId, snapshot.SupplierName, snapshot.SupplierTaxIdentifier),
        snapshot.Currency,
        snapshot.OrderLines.Select(line => new ApprovedPurchaseLine(
            line.SourceQuotationLineId,
            line.SourceRequestedItemId,
            line.Description,
            line.Quantity,
            line.UnitOfMeasure,
            line.UnitPrice)).ToList(),
        DeliveryTerms.Create(snapshot.DeliveryLeadTimeDays, deliveryConditions, deliveryDestination),
        snapshot.InputFingerprint);
}
