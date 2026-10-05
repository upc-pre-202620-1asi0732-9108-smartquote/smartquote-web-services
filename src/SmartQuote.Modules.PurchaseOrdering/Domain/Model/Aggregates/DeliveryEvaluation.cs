using SmartQuote.API.Shared.Domain;
using SmartQuote.API.Shared.Domain.Model.ValueObjects;
using SmartQuote.API.PurchaseOrdering.Domain.Model.ValueObjects;

namespace SmartQuote.API.PurchaseOrdering.Domain.Model.Aggregates;

public class DeliveryEvaluation : AggregateRoot<DeliveryEvaluationId>
{
    public const int MinimumScore = 1;
    public const int MaximumScore = 5;
    public const int MaximumObservationsLength = 500;

    public PurchaseOrderId PurchaseOrderId { get; private set; } = null!;
    public string SupplierTaxIdentifier { get; private set; } = string.Empty;
    public int OnTimeScore { get; private set; }
    public int QualityScore { get; private set; }
    public string? Observations { get; private set; }
    public UserId EvaluatedBy { get; private set; } = null!;
    public DateTimeOffset EvaluatedAt { get; private set; }

    private DeliveryEvaluation() { }

    public static DeliveryEvaluation Create(
        PurchaseOrderId purchaseOrderId,
        string supplierTaxIdentifier,
        int onTimeScore,
        int qualityScore,
        string? observations,
        UserId evaluatedBy,
        DateTimeOffset evaluatedAt)
    {
        ValidateScore(onTimeScore, nameof(onTimeScore));
        ValidateScore(qualityScore, nameof(qualityScore));

        var trimmedObservations = string.IsNullOrWhiteSpace(observations) ? null : observations.Trim();
        if (trimmedObservations is not null && trimmedObservations.Length > MaximumObservationsLength)
            throw new DomainException($"Observations cannot exceed {MaximumObservationsLength} characters.");

        return new DeliveryEvaluation
        {
            Id = new DeliveryEvaluationId(Guid.NewGuid()),
            PurchaseOrderId = purchaseOrderId,
            SupplierTaxIdentifier = supplierTaxIdentifier,
            OnTimeScore = onTimeScore,
            QualityScore = qualityScore,
            Observations = trimmedObservations,
            EvaluatedBy = evaluatedBy,
            EvaluatedAt = evaluatedAt
        };
    }

    private static void ValidateScore(int score, string name)
    {
        if (score < MinimumScore || score > MaximumScore)
            throw new DomainException($"{name} must be between {MinimumScore} and {MaximumScore}.");
    }
}
