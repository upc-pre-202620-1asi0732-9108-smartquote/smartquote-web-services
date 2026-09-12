using SmartQuote.API.SupplyRequests.Domain.Model.Enums;

namespace SmartQuote.API.SupplyRequests.Domain.Model.Commands;

public record RegisterPurchaseRequestCommand(
    DateOnly RequiredDate,
    RequestPriority Priority,
    IReadOnlyList<RequestedItemData> Items);

public record RequestedItemData(
    string Description,
    decimal Quantity,
    string UnitOfMeasure,
    IReadOnlyList<TechnicalRequirementData> Requirements);

public record TechnicalRequirementData(
    string Name,
    ComparisonOperator Operator,
    string ExpectedValue,
    string UnitOfMeasure,
    bool IsMandatory);
