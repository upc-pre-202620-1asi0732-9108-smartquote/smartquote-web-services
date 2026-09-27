using SmartQuote.Modules.EvaluationSimulation.Domain.Model.Aggregates;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.Entities;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.Enums;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.ValueObjects;
using System.Globalization;
using System.Text;

namespace SmartQuote.Modules.EvaluationSimulation.Domain.Services;

public class SimulationEngine
{
    public SimulationRun Run(EvaluationScenario scenario, EvaluationDataset dataset)
    {
        var fingerprint = InputFingerprint.FromParts(dataset.CalculateFingerprint().Value, scenario.CalculateDefinitionFingerprint().Value);

        var run = SimulationRun.Create(scenario.Id, scenario.Version, fingerprint, dataset.Request, dataset.Quotations);

        var evaluations = new List<(QuotationEvaluation Evaluation, CriterionEvaluationInput Input)>();

        foreach (var quotationSnapshot in dataset.Quotations)
        {
            var evaluation = QuotationEvaluation.Create(quotationSnapshot.QuotationId);
            var input = BuildCriterionInput(dataset.Request, quotationSnapshot);

            ApplyMandatoryCriteria(scenario, input, evaluation);

            evaluations.Add((evaluation, input));
            run.AddEvaluation(evaluation);
        }

        ScoreEligibleQuotations(scenario, evaluations);

        foreach (var (evaluation, _) in evaluations)
            evaluation.CalculateTotalScore();

        run.DefineRecommendation();

        return run;
    }

    private static void ApplyMandatoryCriteria(EvaluationScenario scenario, CriterionEvaluationInput input, QuotationEvaluation evaluation)
    {
        foreach (var criterion in scenario.Criteria.Where(c => c.Mode == CriterionMode.Mandatory))
        {
            var result = criterion.Evaluate(input);

            evaluation.AddCriterionResult(result);

            if (!result.Passed)
                evaluation.Exclude(new ExclusionReason(criterion.Id, "MANDATORY_CRITERION_NOT_MET", result.Explanation));
        }
    }

    private static void ScoreEligibleQuotations(EvaluationScenario scenario, List<(QuotationEvaluation Evaluation, CriterionEvaluationInput Input)> evaluations)
    {
        var eligible = evaluations.Where(e => e.Evaluation.IsEligible).ToList();

        foreach (var criterion in scenario.Criteria.Where(c => c.Mode == CriterionMode.Weighted))
        {
            if (criterion.IsNumeric)
                ScoreNumericCriterion(criterion, eligible);
            else
                ScoreCategoricalCriterion(criterion, eligible);
        }
    }

    private static void ScoreNumericCriterion(
        EvaluationCriterion criterion,
        IReadOnlyList<(QuotationEvaluation Evaluation, CriterionEvaluationInput Input)> eligible)
    {
        var actualValues = eligible.Select(e => criterion.ResolveNumericValue(e.Input)).ToList();
        var resolvedValues = actualValues.Where(v => v.HasValue).Select(v => v!.Value).ToList();

        var min = resolvedValues.Count > 0 ? resolvedValues.Min() : 0m;
        var max = resolvedValues.Count > 0 ? resolvedValues.Max() : 0m;

        for (var i = 0; i < eligible.Count; i++)
        {
            var (evaluation, input) = eligible[i];
            var actual = actualValues[i];

            var normalizedScore = actual switch
            {
                null => 0m,
                _ when max == min => 1m,
                _ => criterion.LowerIsBetter ? (max - actual.Value) / (max - min) : (actual.Value - min) / (max - min)
            };

            evaluation.AddCriterionResult(criterion.BuildResult(input, normalizedScore));
        }
    }

    private static void ScoreCategoricalCriterion(
        EvaluationCriterion criterion,
        IReadOnlyList<(QuotationEvaluation Evaluation, CriterionEvaluationInput Input)> eligible)
    {
        foreach (var (evaluation, input) in eligible)
        {
            var normalizedScore = criterion.ResolvePassed(input) ? 1m : 0m;
            evaluation.AddCriterionResult(criterion.BuildResult(input, normalizedScore));
        }
    }

    private static CriterionEvaluationInput BuildCriterionInput(
        RequestEvaluationSnapshot requestSnapshot,
        QuotationEvaluationSnapshot quotationSnapshot)
    {
        var technicalValues = new Dictionary<string, QuotationSpecificationSnapshotData>(StringComparer.OrdinalIgnoreCase);

        foreach (var quotationLine in quotationSnapshot.Lines)
        {
            if (quotationLine.SourceRequestedItemId is null)
                continue;

            var requestedItem = requestSnapshot.Items.FirstOrDefault(item =>
                item.SourceRequestedItemId.Equals(quotationLine.SourceRequestedItemId, StringComparison.OrdinalIgnoreCase));
            if (requestedItem is null)
                continue;

            foreach (var requirement in requestedItem.Requirements.Where(requirement => requirement.SourceRequirementId is not null))
            {
                var matches = quotationLine.Specifications.Where(candidate =>
                    NormalizeTechnicalName(candidate.Name) == NormalizeTechnicalName(requirement.Name)).ToList();
                if (matches.Count == 1)
                    technicalValues[requirement.SourceRequirementId!] = matches[0];
            }
        }

        return new CriterionEvaluationInput(
            quotationSnapshot.TotalPrice().Amount,
            quotationSnapshot.DeliveryLeadTimeDays,
            technicalValues);
    }

    private static string NormalizeTechnicalName(string name)
    {
        var decomposed = name.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var plain = new string(decomposed.Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : ' ').ToArray());
        var tokens = plain.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(token => token is not ("minima" or "minimo" or "maxima" or "maximo" or "cruda" or "crude" or "contenido" or "porcentaje" or "de"))
            .Select(token => token == "protein" ? "proteina" : token);
        return string.Join(' ', tokens);
    }
}
