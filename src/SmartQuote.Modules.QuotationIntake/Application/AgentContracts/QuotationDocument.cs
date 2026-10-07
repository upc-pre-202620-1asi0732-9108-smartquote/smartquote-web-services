namespace SmartQuote.Modules.QuotationIntake.Application.AgentContracts;

public record QuotationDocument(string FileName, string ContentType, byte[] Content);
