using SmartQuote.API.SupplyRequests.Application.Views;
using SmartQuote.API.SupplyRequests.Interfaces.REST.Resources;

namespace SmartQuote.API.SupplyRequests.Interfaces.REST.Transform;

public static class PurchaseRequestResourceFromViewAssembler
{
    public static PurchaseRequestResource ToResource(PurchaseRequestView view) =>
        new(
            view.RequestId,
            view.RequesterId,
            view.RequiredDate,
            view.Priority,
            view.Status,
            view.NextResponsibleArea,
            view.Version,
            view.CreatedAt,
            view.UpdatedAt,
            view.Items.Select(ToItemResource).ToList(),
            view.Attachments.Select(ToAttachmentResource).ToList());

    private static RequestedItemDetailResource ToItemResource(RequestedItemView view) =>
        new(
            view.ItemId,
            view.LineNumber,
            view.Description,
            view.Quantity,
            view.UnitOfMeasure,
            view.Requirements.Select(ToRequirementResource).ToList());

    private static TechnicalRequirementResource ToRequirementResource(TechnicalRequirementView view) =>
        new(view.RequirementId, view.Name, view.Operator, view.ExpectedValue, view.UnitOfMeasure, view.IsMandatory);

    private static RequestAttachmentResource ToAttachmentResource(RequestAttachmentView view) =>
        new(view.AttachmentId, view.FileName, view.ContentType, view.UploadedBy, view.UploadedAt);
}
