namespace SmartQuote.Modules.QuotationIntake.Domain.Model.ValueObjects;

public record ExtractedFieldData(
    string FieldPath,
    string? Value,
    decimal Confidence,
    int PageNumber,
    string TextReference,
    bool IsResolved);
