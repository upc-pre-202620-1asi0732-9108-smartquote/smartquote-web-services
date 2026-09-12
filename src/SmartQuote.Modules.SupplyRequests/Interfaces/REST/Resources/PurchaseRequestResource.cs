namespace SmartQuote.API.SupplyRequests.Interfaces.REST.Resources;

public record PurchaseRequestResource(
    Guid RequestId,
    Guid RequesterId,
    DateOnly RequiredDate,
    string Priority,
    string Status,
    string NextResponsibleArea,
    long Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<RequestedItemDetailResource> Items,
    IReadOnlyList<RequestAttachmentResource> Attachments);

public record RequestedItemDetailResource(
    Guid ItemId,
    int LineNumber,
    string Description,
    decimal Quantity,
    string UnitOfMeasure,
    IReadOnlyList<TechnicalRequirementResource> Requirements);

public record TechnicalRequirementResource(
    Guid RequirementId,
    string Name,
    string Operator,
    string ExpectedValue,
    string UnitOfMeasure,
    bool IsMandatory);

public record RequestAttachmentResource(
    Guid AttachmentId,
    string FileName,
    string ContentType,
    Guid UploadedBy,
    DateTimeOffset UploadedAt);
