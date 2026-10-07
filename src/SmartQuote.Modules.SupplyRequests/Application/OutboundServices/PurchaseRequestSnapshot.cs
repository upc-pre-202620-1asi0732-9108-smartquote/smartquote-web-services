namespace SmartQuote.API.SupplyRequests.Application.OutboundServices;

public record PurchaseRequestSnapshot(
    string RequestId,
    long Version,
    string Status,
    string RequesterId,
    DateOnly RequiredDate,
    string Priority,
    IReadOnlyList<RequestedItemSnapshot> Items);

public record RequestedItemSnapshot(
    string ItemId,
    int LineNumber,
    string Description,
    decimal Quantity,
    string UnitOfMeasure,
    IReadOnlyList<TechnicalRequirementSnapshot> Requirements);

public record TechnicalRequirementSnapshot(
    string RequirementId,
    string Name,
    string Operator,
    string ExpectedValue,
    string UnitOfMeasure,
    bool IsMandatory);
