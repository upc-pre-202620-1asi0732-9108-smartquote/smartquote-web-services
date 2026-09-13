namespace SmartQuote.Modules.EvaluationSimulation.Domain.Model.ValueObjects;

public record CriterionEvaluationInput(
    decimal TotalPrice,
    int DeliveryLeadTimeDays,
    IReadOnlyDictionary<string, QuotationSpecificationSnapshotData> TechnicalValuesByRequirementId);
