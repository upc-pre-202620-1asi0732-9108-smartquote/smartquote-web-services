namespace SmartQuote.Modules.EvaluationSimulation.Domain.Model.ValueObjects;

public class Recommendation
{
    public string QuotationId { get; private set; } = string.Empty;
    public Score Score { get; private set; } = new(0);
    public string Explanation { get; private set; } = string.Empty;

    private Recommendation() { }

    public Recommendation(string quotationId, Score score, string explanation)
    {
        QuotationId = quotationId;
        Score = score;
        Explanation = explanation;
    }
}
