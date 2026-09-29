namespace SmartQuote.Modules.EvaluationSimulation.Domain.Model.ValueObjects;

public record EvaluationDataset(RequestEvaluationSnapshot Request, IReadOnlyList<QuotationEvaluationSnapshot> Quotations)
{
    public InputFingerprint CalculateFingerprint(ExchangeRateSnapshot? exchangeRate = null) =>
        InputFingerprint.FromParts(
            System.Text.Json.JsonSerializer.Serialize(new
            {
                Request.RequestId, Request.RequiredDate, Request.Priority,
                Items = Request.Items.OrderBy(item => item.LineNumber).Select(item => new
                {
                    item.SourceRequestedItemId, item.LineNumber, item.Description, item.Quantity, item.UnitOfMeasure,
                    Requirements = item.Requirements.OrderBy(requirement => requirement.SourceRequirementId)
                })
            }),
            string.Join(',', Quotations.OrderBy(q => q.QuotationId).Select(q => $"{q.QuotationId}:{q.Version}")),
            exchangeRate?.FingerprintPart() ?? "NO_CURRENCY_CONVERSION");
}
