namespace SmartQuote.Modules.QuotationIntake.Domain.Model.Commands;

public record CorrectFieldCommand(
    Guid QuotationId,
    Guid FieldId,
    string Value,
    string Reason,
    long ExpectedVersion);
