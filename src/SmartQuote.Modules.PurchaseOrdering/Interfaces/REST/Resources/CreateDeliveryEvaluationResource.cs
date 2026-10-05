using System.ComponentModel.DataAnnotations;
using SmartQuote.API.PurchaseOrdering.Domain.Model.Aggregates;

namespace SmartQuote.API.PurchaseOrdering.Interfaces.REST.Resources;

public sealed record CreateDeliveryEvaluationResource(
    [Range(DeliveryEvaluation.MinimumScore, DeliveryEvaluation.MaximumScore)] int OnTimeScore,
    [Range(DeliveryEvaluation.MinimumScore, DeliveryEvaluation.MaximumScore)] int QualityScore,
    [StringLength(DeliveryEvaluation.MaximumObservationsLength)] string? Observations);
