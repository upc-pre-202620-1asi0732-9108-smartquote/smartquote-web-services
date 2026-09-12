using SmartQuote.API.SupplyRequests.Domain.Model.Commands;
using SmartQuote.API.SupplyRequests.Domain.Model.Enums;
using SmartQuote.API.SupplyRequests.Interfaces.REST.Resources;

namespace SmartQuote.API.SupplyRequests.Interfaces.REST.Transform;

public static class CreatePurchaseRequestCommandFromResourceAssembler
{
    public static RegisterPurchaseRequestCommand ToCommand(CreatePurchaseRequestResource resource) =>
        new(
            resource.RequiredDate,
            EnumResourceParser.Parse<RequestPriority>(resource.Priority, nameof(resource.Priority)),
            resource.Items.Select(ToItemData).ToList());

    private static RequestedItemData ToItemData(RequestedItemResource resource) =>
        new(
            resource.Description,
            resource.Quantity,
            resource.UnitOfMeasure,
            resource.Requirements.Select(ToRequirementData).ToList());

    private static TechnicalRequirementData ToRequirementData(TechnicalRequirementInputResource resource) =>
        new(
            resource.Name,
            EnumResourceParser.Parse<ComparisonOperator>(resource.Operator, nameof(resource.Operator)),
            resource.ExpectedValue,
            resource.UnitOfMeasure,
            resource.IsMandatory);
}
