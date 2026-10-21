namespace SmartQuote.Modules.EvaluationSimulation.Domain.Model.ValueObjects;

public record EvaluationDataset(RequestEvaluationSnapshot Request, IReadOnlyList<QuotationEvaluationSnapshot> Quotations)
{
    public InputFingerprint CalculateFingerprint(ExchangeRateSnapshot? exchangeRate = null)
    {
        var fingerprint = InputFingerprint.FromParts(
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
        // Revision zero preserves existing fingerprints. Administrative assignment is intentionally excluded.
        if (Request.TechnicalRevision != 0)
            fingerprint = InputFingerprint.FromParts(fingerprint.Value, Request.TechnicalRevision.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (Quotations.Any(q => q.SupplierEligibilityStatus is not null))
            fingerprint = InputFingerprint.FromParts(fingerprint.Value, System.Text.Json.JsonSerializer.Serialize(Quotations
                .OrderBy(q => q.QuotationId).Select(q => new { q.SupplierAccountId, q.SupplierEligibilityStatus, q.SupplierEligibilityVersion })));
        return fingerprint;
    }
}
