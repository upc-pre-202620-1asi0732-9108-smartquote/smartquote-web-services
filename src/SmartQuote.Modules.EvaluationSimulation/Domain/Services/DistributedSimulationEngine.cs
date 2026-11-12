using SmartQuote.API.Shared.Domain;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.Aggregates;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.Enums;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.ValueObjects;

namespace SmartQuote.Modules.EvaluationSimulation.Domain.Services;

/// <summary>US21: full-quantity, one-winner-per-item evaluation; not quantity splitting or a global optimizer.</summary>
public sealed class DistributedSimulationEngine(SimulationEngine scorer)
{
    public DistributedSimulationResult Evaluate(EvaluationScenario scenario, EvaluationDataset dataset, ExchangeRateSnapshot? rate, DateOnly today)
    {
        if (dataset.Request.Items.Count < 2) throw new DomainException("Distributed purchasing requires at least two requested items.");
        if (dataset.Quotations.Count(q => !q.ValidUntil.HasValue || q.ValidUntil >= today) < 2)
            throw new DomainException("At least two current verified quotations are required.");
        var allocations = new List<DistributedAllocation>(); var candidates = new List<DistributedCandidate>(); var uncovered = new List<string>();
        foreach (var item in dataset.Request.Items)
        {
            var applicable = scenario.Criteria.Where(c => c.Category != CriterionCategory.TechnicalCompliance ||
                item.Requirements.Any(r => r.SourceRequirementId == c.TargetField)).ToList();
            foreach (var requirement in item.Requirements.Where(r => r.IsMandatory))
                if (!applicable.Any(c => c.Mode == CriterionMode.Mandatory && c.TargetField == requirement.SourceRequirementId &&
                    c.Operator == requirement.Operator && c.ExpectedValue == requirement.ExpectedValue && c.UnitOfMeasure == requirement.UnitOfMeasure))
                    throw new DomainException("Every mandatory item requirement must be represented by the current scenario.");
            if (applicable.Where(c => c.Mode == CriterionMode.Weighted).Sum(c => c.Weight) != 100)
                throw new DomainException("Distributed scenarios must have 100% comparable commercial weights for every item; use technical requirements as mandatory constraints.");
            var scopedRequest = new RequestEvaluationSnapshot(dataset.Request.RequestId, dataset.Request.Version,
                dataset.Request.RequiredDate, dataset.Request.Priority, [item], dataset.Request.CapturedAt, dataset.Request.TechnicalRevision);
            var eligibleQuotes = new List<QuotationEvaluationSnapshot>();
            foreach (var q in dataset.Quotations)
            {
                var matches = q.Lines.Where(l => l.SourceRequestedItemId == item.SourceRequestedItemId).ToList();
                var reasons = new List<string>();
                if (matches.Count != 1) reasons.Add("Se requiere exactamente una línea verificada y asociada a este ítem.");
                else
                {
                    if (matches[0].Quantity < item.Quantity) reasons.Add("La cantidad ofertada no cubre íntegramente la cantidad solicitada.");
                    if (!matches[0].UnitOfMeasure.Trim().Equals(item.UnitOfMeasure.Trim(), StringComparison.OrdinalIgnoreCase)) reasons.Add("La unidad no coincide; no se realizan conversiones implícitas.");
                }
                if (reasons.Count > 0) { candidates.Add(new(item.SourceRequestedItemId, q.QuotationId, matches.FirstOrDefault()?.SourceQuotationLineId, false, 0, reasons)); continue; }
                var line = matches[0];
                eligibleQuotes.Add(new(q.QuotationId, q.Version, q.SupplierId, q.SupplierBusinessName, q.SupplierTaxIdentifier, q.Currency,
                    q.DeliveryLeadTimeDays, q.VerifiedAt, [new(line.SourceQuotationLineId, line.SourceRequestedItemId, line.LineNumber,
                        line.Description, item.Quantity, line.UnitOfMeasure, line.UnitPrice, line.Specifications)], q.CapturedAt,
                    q.SupplierAccountId, q.SupplierEligibilityStatus, q.SupplierEligibilityVersion, q.ValidUntil));
            }
            var scored = scorer.EvaluateCandidates(applicable, new(scopedRequest, eligibleQuotes), rate, today);
            foreach (var e in scored) candidates.Add(new(item.SourceRequestedItemId, e.QuotationId,
                eligibleQuotes.Single(q => q.QuotationId == e.QuotationId).Lines[0].SourceQuotationLineId, e.IsEligible,
                e.TotalScore.Value, e.ExclusionReasons.Select(r => r.Explanation).ToList()));
            var winner = scored.Where(e => e.IsEligible).OrderByDescending(e => e.TotalScore.Value).ThenBy(e => e.QuotationId, StringComparer.Ordinal).FirstOrDefault();
            if (winner is null) { uncovered.Add(item.SourceRequestedItemId); continue; }
            var selected = eligibleQuotes.Single(q => q.QuotationId == winner.QuotationId); var selectedLine = selected.Lines[0];
            var unitPen = selected.Currency == "PEN" ? selectedLine.UnitPrice : rate?.Rate * selectedLine.UnitPrice
                ?? throw new DomainException("USD requires preserved official exchange-rate evidence.");
            allocations.Add(new(item.SourceRequestedItemId, selected.QuotationId, selectedLine.SourceQuotationLineId!, selected.SupplierAccountId!.Value,
                selected.SupplierEligibilityVersion, selected.SupplierBusinessName, selected.SupplierTaxIdentifier, item.Description, item.Quantity,
                item.UnitOfMeasure, selected.Currency, selectedLine.UnitPrice, item.Quantity * selectedLine.UnitPrice, unitPen,
                decimal.Round(item.Quantity * unitPen, 4, MidpointRounding.AwayFromZero), winner.TotalScore.Value, selected.DeliveryLeadTimeDays));
        }
        return new(uncovered.Count == 0, uncovered, allocations, candidates, allocations.Sum(a => a.AmountPen));
    }
}
