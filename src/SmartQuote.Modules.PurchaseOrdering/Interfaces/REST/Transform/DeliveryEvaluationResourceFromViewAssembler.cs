using SmartQuote.API.PurchaseOrdering.Application.Views;
using SmartQuote.API.PurchaseOrdering.Interfaces.REST.Resources;

namespace SmartQuote.API.PurchaseOrdering.Interfaces.REST.Transform;

public static class DeliveryEvaluationResourceFromViewAssembler
{
    public static DeliveryEvaluationResource ToResource(DeliveryEvaluationView view) =>
        new(view.DeliveryEvaluationId, view.PurchaseOrderId, view.SupplierTaxIdentifier, view.OnTimeScore,
            view.QualityScore, view.Observations, view.EvaluatedBy, view.EvaluatedAt);

    public static SupplierPerformanceResource ToResource(SupplierPerformanceView view) =>
        new(view.SupplierTaxIdentifier, view.EvaluationCount, view.AverageOnTimeScore, view.AverageQualityScore,
            view.OverallScore, view.FirstEvaluatedAt, view.LastEvaluatedAt);
}
