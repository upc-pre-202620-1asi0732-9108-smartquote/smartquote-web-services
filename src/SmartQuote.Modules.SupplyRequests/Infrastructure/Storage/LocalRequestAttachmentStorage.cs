using Microsoft.Extensions.Hosting;
using SmartQuote.API.SupplyRequests.Application.Ports;

namespace SmartQuote.API.SupplyRequests.Infrastructure.Storage;

public sealed class LocalRequestAttachmentStorage(IHostEnvironment environment) : IRequestAttachmentStorage
{
    private string RootDirectory => Path.Combine(environment.ContentRootPath, "App_Data", "request-attachments");

    public async Task<string> SaveAsync(string fileName, byte[] content, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(RootDirectory);
        var storageKey = $"{Guid.NewGuid():N}_{Path.GetFileName(fileName)}";
        await File.WriteAllBytesAsync(Path.Combine(RootDirectory, storageKey), content, cancellationToken);
        return storageKey;
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
