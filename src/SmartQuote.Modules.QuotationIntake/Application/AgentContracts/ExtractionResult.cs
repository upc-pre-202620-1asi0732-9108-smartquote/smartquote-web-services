namespace SmartQuote.Modules.QuotationIntake.Application.AgentContracts;

public record ExtractionResult(
    string? Supplier,
    DateOnly? ValidUntil,
    string? Currency,
    int? DeliveryLeadTimeDays,
    IReadOnlyList<ExtractedLineResult> Lines,
    IReadOnlyList<ExtractedFieldResult> Fields);

public record ExtractedLineResult(
    string? Description,
    decimal? Quantity,
    string? UnitOfMeasure,
    decimal? UnitPrice,
    IReadOnlyList<ExtractedSpecificationResult> Specifications);

public record ExtractedSpecificationResult(string Name, string Value, string UnitOfMeasure);

public record ExtractedFieldResult(
    string FieldPath,
    string? Value,
    decimal Confidence,
    int PageNumber,
    string TextReference,
    bool IsResolved);
