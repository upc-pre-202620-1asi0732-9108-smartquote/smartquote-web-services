namespace SmartQuote.Modules.QuotationIntake.Domain.Model.ValueObjects;

public record ExtractedQuotationData(
    SupplierReference Supplier,
    DateOnly? ValidUntil,
    string? Currency,
    int? DeliveryLeadTimeDays,
    IReadOnlyList<ExtractedLineData> Lines,
    IReadOnlyList<ExtractedFieldData> Fields);
