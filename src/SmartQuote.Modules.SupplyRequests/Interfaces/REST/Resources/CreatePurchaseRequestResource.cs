namespace SmartQuote.API.SupplyRequests.Interfaces.REST.Resources;

public record CreatePurchaseRequestResource(
    DateOnly RequiredDate,
    string Priority,
    IReadOnlyList<RequestedItemResource> Items);

public record RequestedItemResource(
    string Description,
    decimal Quantity,
    string UnitOfMeasure,
    IReadOnlyList<TechnicalRequirementInputResource> Requirements);

public record TechnicalRequirementInputResource(
    string Name,
    string Operator,
    string ExpectedValue,
    string UnitOfMeasure,
    bool IsMandatory);
