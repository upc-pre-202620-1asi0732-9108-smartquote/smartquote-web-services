namespace SmartQuote.Modules.QuotationIntake.Interfaces.REST.Resource;

public record PoultryQuoteResource(
    Guid QuotationId,
    string RequestId,
    string SupplierId,
    string SupplierBusinessName,
    string SupplierTaxIdentifier,
    string FileName,
    DateOnly? ValidUntil,
    string? Currency,
    int? DeliveryLeadTimeDays,
    string Status,
    long Version,
    Guid? VerifiedBy,
    DateTimeOffset? VerifiedAt,
    string? RejectionReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<QuotationLineResource> Lines,
    IReadOnlyList<ExtractedFieldResource> Fields);

public record QuotationLineResource(
    Guid LineId,
    string? RequestedItemId,
    int LineNumber,
    string? Description,
    decimal? Quantity,
    string? UnitOfMeasure,
    decimal? UnitPrice,
    IReadOnlyList<QuotedSpecificationResource> Specifications);

public record QuotedSpecificationResource(string Name, string Value, string UnitOfMeasure);

public record ExtractedFieldResource(
    Guid FieldId,
    string FieldPath,
    string? OriginalValue,
    string? CurrentValue,
    bool IsRequired,
    decimal Confidence,
    int SourcePageNumber,
    string SourceTextReference,
    string Status,
    IReadOnlyList<FieldCorrectionResource> Corrections);

public record FieldCorrectionResource(string PreviousValue, string CorrectedValue, Guid CorrectedBy, DateTimeOffset CorrectedAt, string Reason);
