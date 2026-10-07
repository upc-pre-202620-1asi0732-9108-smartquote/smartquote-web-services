using SmartQuote.Modules.QuotationIntake.Application.Ports;
using SmartQuote.API.SupplyRequests.Application.Ports;

namespace SmartQuote.Integration.Tests;

// Archivos aislados por host de pruebas; nunca se accede al App_Data de demostración.
public sealed class TemporaryDocumentStorage : IQuotationDocumentStorage, IRequestAttachmentStorage, IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "smartquote-test-documents-" + Guid.NewGuid().ToString("N"));
    public Task<string> SaveAsync(string fileName, string contentType, byte[] content, CancellationToken cancellationToken = default) => SaveAsync(fileName, content, cancellationToken);
    public async Task<string> SaveAsync(string fileName, byte[] content, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_root);
        var key = Guid.NewGuid().ToString("N") + "_" + Path.GetFileName(fileName);
        await File.WriteAllBytesAsync(Path.Combine(_root, key), content, cancellationToken);
        return key;
    }
    public Task<byte[]> GetAsync(string storageKey, CancellationToken cancellationToken = default) => File.ReadAllBytesAsync(Path.Combine(_root, Path.GetFileName(storageKey)), cancellationToken);
    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        File.Delete(Path.Combine(_root, Path.GetFileName(storageKey)));
        return Task.CompletedTask;
    }
    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
