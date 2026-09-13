using SmartQuote.API.PurchaseOrdering.Domain.Model.Aggregates;
using SmartQuote.API.PurchaseOrdering.Domain.Model.ValueObjects;

namespace SmartQuote.API.PurchaseOrdering.Domain.Services;

public class PurchaseOrderGenerator
{
    public PurchaseOrder Generate(ApprovedPurchaseDecision decision, Approval approval, OrderNumber orderNumber) =>
        PurchaseOrder.Create(orderNumber, decision, approval);
}
