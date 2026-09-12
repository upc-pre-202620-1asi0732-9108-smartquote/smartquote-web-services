namespace SmartQuote.API.SupplyRequests.Application.Views;

public record PurchaseRequestView(
    Guid RequestId,
    Guid RequesterId,
    DateOnly RequiredDate,
    string Priority,
    string Status,
    string NextResponsibleArea,
    long Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<RequestedItemView> Items,
    IReadOnlyList<RequestAttachmentView> Attachments);

public record RequestedItemView(
    Guid ItemId,
    int LineNumber,
    string Description,
    decimal Quantity,
    string UnitOfMeasure,
    IReadOnlyList<TechnicalRequirementView> Requirements);

public record TechnicalRequirementView(
    Guid RequirementId,
    string Name,
    string Operator,
    string ExpectedValue,
    string UnitOfMeasure,
    bool IsMandatory);

public record RequestAttachmentView(
    Guid AttachmentId,
    string FileName,
    string ContentType,
    Guid UploadedBy,
    DateTimeOffset UploadedAt);
