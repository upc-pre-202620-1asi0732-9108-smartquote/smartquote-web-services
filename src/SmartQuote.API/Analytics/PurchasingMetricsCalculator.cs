using SmartQuote.Modules.EvaluationSimulation.Domain.Model.Aggregates;

namespace SmartQuote.API.Analytics;

public static class PurchasingMetricsCalculator
{
    public static decimal? ComparativeSavingsPen(SimulationRun run, string selectedQuotationId)
    {
        var eligibleIds = run.Evaluations
            .Where(evaluation => evaluation.IsEligible)
            .Select(evaluation => evaluation.QuotationId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (eligibleIds.Count < 2 || !eligibleIds.Contains(selectedQuotationId))
            return null;

        var comparable = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var quotation in run.QuotationSnapshots.Where(q => eligibleIds.Contains(q.QuotationId)))
        {
            var total = quotation.TotalPrice();
            if (total.Currency == "USD" &&
                (run.ExchangeRate is null || run.ExchangeRate.SourceCurrency != "USD" ||
                 run.ExchangeRate.TargetCurrency != "PEN"))
                return null;
            if (total.Currency is not ("PEN" or "USD"))
                return null;

            comparable[quotation.QuotationId] = total.Currency == "PEN"
                ? total.Amount
                : run.ExchangeRate!.Convert(total).Amount;
        }

        if (comparable.Count != eligibleIds.Count || !comparable.TryGetValue(selectedQuotationId, out var selected))
            return null;

        var alternatives = comparable.Where(pair => pair.Key != selectedQuotationId).Select(pair => pair.Value).ToArray();
        if (alternatives.Length == 0)
            return null;

        return Math.Max(0m, alternatives.Min() - selected);
    }
}
