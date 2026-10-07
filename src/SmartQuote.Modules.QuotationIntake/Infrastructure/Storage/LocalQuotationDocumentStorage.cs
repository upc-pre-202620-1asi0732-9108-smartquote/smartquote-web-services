using Microsoft.Extensions.Hosting;
using SmartQuote.Modules.QuotationIntake.Application.Ports;

namespace SmartQuote.Modules.QuotationIntake.Infrastructure.Storage;

public class LocalQuotationDocumentStorage(IHostEnvironment environment) : IQuotationDocumentStorage
{
    private string RootDirectory => Path.Combine(environment.ContentRootPath, "App_Data", "quotation-documents");

    public async Task<string> SaveAsync(string fileName, string contentType, byte[] content, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(RootDirectory);

        var storageKey = $"{Guid.NewGuid()}_{Path.GetFileName(fileName)}";
        var path = Path.Combine(RootDirectory, storageKey);

        await File.WriteAllBytesAsync(path, content, cancellationToken);

        return storageKey;
    }

    public async Task<byte[]> GetAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var safeKey = Path.GetFileName(storageKey);
        if (!string.Equals(safeKey, storageKey, StringComparison.Ordinal))
            throw new InvalidOperationException("Invalid document storage key.");

        var path = Path.Combine(RootDirectory, safeKey);
        return await File.ReadAllBytesAsync(path, cancellationToken);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var safeKey = Path.GetFileName(storageKey);
        var path = Path.Combine(RootDirectory, safeKey);
        if (File.Exists(path))
            File.Delete(path);

        return Task.CompletedTask;
    }
}

