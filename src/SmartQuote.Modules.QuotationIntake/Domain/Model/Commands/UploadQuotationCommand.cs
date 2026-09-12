namespace SmartQuote.Modules.QuotationIntake.Domain.Model.Commands;

public record UploadQuotationCommand(
    Guid RequestId,
    string SupplierId,
    string SupplierBusinessName,
    string SupplierTaxIdentifier,
    string FileName,
    string ContentType,
    byte[] FileContent);
