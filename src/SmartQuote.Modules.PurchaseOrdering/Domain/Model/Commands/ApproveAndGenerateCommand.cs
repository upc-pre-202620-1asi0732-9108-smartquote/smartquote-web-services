namespace SmartQuote.API.PurchaseOrdering.Domain.Model.Commands;

/// <summary>
/// DeliveryConditions/DeliveryDestination are supplied here rather than sourced from the simulation
/// snapshot: they are a logistics decision the approver makes at issuance time, not something
/// extracted from a quotation or an evaluation criterion (ApprovedSimulationSnapshot.DeliveryTerms
/// only carries the lead time as a string per the class diagram).
/// </summary>
public record ApproveAndGenerateCommand(
    Guid SimulationRunId,
    string QuotationId,
    string DeliveryConditions,
    string DeliveryDestination);
