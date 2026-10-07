using SmartQuote.API.Shared.Domain;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.ValueObjects;

namespace SmartQuote.Modules.EvaluationSimulation.Domain.Model.Entities;

public class QuotationEvaluation
{
    private readonly List<CriterionResult> _criterionResults = [];
    private readonly List<ExclusionReason> _exclusionReasons = [];

    public string QuotationId { get; private set; } = string.Empty;
    public bool IsEligible { get; private set; } = true;
    public Score TotalScore { get; private set; } = new(0);
    public int? Rank { get; private set; }

    public IReadOnlyList<CriterionResult> CriterionResults => _criterionResults.AsReadOnly();
    public IReadOnlyList<ExclusionReason> ExclusionReasons => _exclusionReasons.AsReadOnly();

    private QuotationEvaluation() { }

    private QuotationEvaluation(string quotationId)
    {
        QuotationId = quotationId;
    }

    public static QuotationEvaluation Create(string quotationId)
    {
        if (string.IsNullOrWhiteSpace(quotationId))
            throw new DomainException("Quotation id is required to build an evaluation.");

        return new QuotationEvaluation(quotationId);
    }

    public void Exclude(ExclusionReason reason)
    {
        IsEligible = false;
        _exclusionReasons.Add(reason);
    }

    public void AddCriterionResult(CriterionResult result) => _criterionResults.Add(result);

    public void CalculateTotalScore() => TotalScore = new Score(_criterionResults.Sum(result => result.WeightedContribution));

    internal void AssignRank(int rank) => Rank = rank;
}
