namespace SmartQuote.Modules.EvaluationSimulation.Domain.Model.ValueObjects;

public record EvaluationDataset(RequestEvaluationSnapshot Request, IReadOnlyList<QuotationEvaluationSnapshot> Quotations)
{
    public InputFingerprint CalculateFingerprint() =>
        InputFingerprint.FromParts(
            $"{Request.RequestId}:{Request.Version}",
            string.Join(',', Quotations.OrderBy(q => q.QuotationId).Select(q => $"{q.QuotationId}:{q.Version}")));
}
