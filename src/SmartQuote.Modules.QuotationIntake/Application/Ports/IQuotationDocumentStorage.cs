namespace SmartQuote.Modules.QuotationIntake.Application.Ports;

public interface IQuotationDocumentStorage
{
    Task<string> SaveAsync(string fileName, string contentType, byte[] content, CancellationToken cancellationToken = default);

    Task<byte[]> GetAsync(string storageKey, CancellationToken cancellationToken = default);
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default);
}
