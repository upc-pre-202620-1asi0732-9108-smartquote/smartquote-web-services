namespace SmartQuote.API.SupplyRequests.Application.Ports;

public interface IRequestAttachmentStorage
{
    Task<string> SaveAsync(string fileName, byte[] content, CancellationToken cancellationToken = default);
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default);
}
