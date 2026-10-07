namespace SmartQuote.Modules.QuotationIntake.Application.Views;

public record PoultryQuoteView(
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
    IReadOnlyList<QuotationLineView> Lines,
    IReadOnlyList<ExtractedFieldView> Fields);

public record QuotationLineView(
    Guid LineId,
    string? RequestedItemId,
    int LineNumber,
    string? Description,
    decimal? Quantity,
    string? UnitOfMeasure,
    decimal? UnitPrice,
    IReadOnlyList<QuotedSpecificationView> Specifications);

public record QuotedSpecificationView(string Name, string Value, string UnitOfMeasure);

public record ExtractedFieldView(
    Guid FieldId,
    string FieldPath,
    string? OriginalValue,
    string? CurrentValue,
    bool IsRequired,
    decimal Confidence,
    int SourcePageNumber,
    string SourceTextReference,
    string Status,
    IReadOnlyList<FieldCorrectionView> Corrections);

public record FieldCorrectionView(string PreviousValue, string CorrectedValue, Guid CorrectedBy, DateTimeOffset CorrectedAt, string Reason);
