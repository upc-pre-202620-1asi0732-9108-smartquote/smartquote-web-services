using SmartQuote.API.PurchaseOrdering.Application.Views;
using SmartQuote.API.PurchaseOrdering.Interfaces.REST.Resources;

namespace SmartQuote.API.PurchaseOrdering.Interfaces.REST.Transform;

public static class PurchaseOrderResourceFromViewAssembler
{
    public static PurchaseOrderResource ToResource(PurchaseOrderView view) =>
        new(
            view.PurchaseOrderId,
            view.OrderNumber,
            view.SimulationRunId,
            view.PurchaseRequestId,
            view.QuotationId,
            view.InputFingerprint,
            view.SupplierId,
            view.SupplierBusinessName,
            view.SupplierTaxIdentifier,
            view.ApprovedBy,
            view.ApprovedAt,
            view.Status,
            view.Currency,
            view.DeliveryLeadTimeDays,
            view.DeliveryConditions,
            view.DeliveryDestination,
            view.Total,
            view.CreatedAt,
            view.Lines.Select(line => new PurchaseOrderLineResource(
                line.LineId,
                line.LineNumber,
                line.SourceQuotationLineId,
                line.SourceRequestedItemId,
                line.Description,
                line.Quantity,
                line.UnitOfMeasure,
                line.UnitPrice)).ToList());
}
