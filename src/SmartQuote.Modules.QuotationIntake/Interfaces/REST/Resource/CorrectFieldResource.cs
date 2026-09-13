namespace SmartQuote.Modules.QuotationIntake.Interfaces.REST.Resource;

public record CorrectFieldResource(string Value, string Reason, long ExpectedVersion);
