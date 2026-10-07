namespace SmartQuote.Modules.QuotationIntake.Interfaces.REST.Resource;

public sealed record BatchQuotationUploadItemResource(
    string FileName,
    Guid? QuotationId,
    bool WasCreated,
    string? ErrorCode,
    string? Error);
