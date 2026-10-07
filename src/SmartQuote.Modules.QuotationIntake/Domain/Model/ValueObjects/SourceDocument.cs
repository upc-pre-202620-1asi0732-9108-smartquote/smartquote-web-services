using SmartQuote.API.Shared.Domain;

namespace SmartQuote.Modules.QuotationIntake.Domain.Model.ValueObjects;

public record SourceDocument
{
    /// <summary>
    /// Only PDF is a valid quotation document (report section 1.2.1, US04 scenario 2, TS01 scenario 3:
    /// suppliers send quotations in PDF; "formato no permitido" is rejected at incorporation time).
    /// </summary>
    public const string AllowedContentType = "application/pdf";

    public string FileName { get; }
    public string ContentType { get; }
    public string StorageKey { get; }
    public string Sha256Hash { get; }

    public static bool IsSupportedContentType(string contentType) =>
        string.Equals(contentType, AllowedContentType, StringComparison.OrdinalIgnoreCase);

    public static bool IsSupportedUploadContentType(string? contentType) =>
        string.IsNullOrWhiteSpace(contentType) ||
        IsSupportedContentType(contentType) ||
        string.Equals(contentType, "application/octet-stream", StringComparison.OrdinalIgnoreCase);

    public SourceDocument(string fileName, string contentType, string storageKey, string sha256Hash)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new DomainException("Source document file name is required.");

        if (!IsSupportedContentType(contentType))
            throw new DomainException($"Unsupported file type '{contentType}'. Only PDF quotations are accepted.");

        if (string.IsNullOrWhiteSpace(storageKey))
            throw new DomainException("Source document storage key is required.");

        if (string.IsNullOrWhiteSpace(sha256Hash) || sha256Hash.Length != 64)
            throw new DomainException("Source document SHA-256 hash must be 64 hex characters.");

        FileName = fileName;
        ContentType = contentType;
        StorageKey = storageKey;
        Sha256Hash = sha256Hash;
    }
}
