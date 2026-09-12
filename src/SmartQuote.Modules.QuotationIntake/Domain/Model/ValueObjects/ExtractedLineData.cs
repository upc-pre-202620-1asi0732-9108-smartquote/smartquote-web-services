namespace SmartQuote.Modules.QuotationIntake.Domain.Model.ValueObjects;

public record ExtractedLineData(
    string? Description,
    decimal? Quantity,
    string? UnitOfMeasure,
    decimal? UnitPrice,
    IReadOnlyList<QuotedSpecification> Specifications);
