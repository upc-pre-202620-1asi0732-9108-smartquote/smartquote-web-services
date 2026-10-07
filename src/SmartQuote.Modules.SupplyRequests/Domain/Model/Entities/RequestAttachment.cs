using SmartQuote.API.Shared.Domain;
using SmartQuote.API.Shared.Domain.Model.ValueObjects;
using SmartQuote.API.SupplyRequests.Domain.Model.ValueObjects;

namespace SmartQuote.API.SupplyRequests.Domain.Model.Entities;

public class RequestAttachment
{
    public RequestAttachmentId Id { get; private set; } = null!;
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public string StorageKey { get; private set; } = string.Empty;
    public UserId UploadedBy { get; private set; } = null!;
    public DateTimeOffset UploadedAt { get; private set; }

    private RequestAttachment() { }

    private RequestAttachment(
        RequestAttachmentId id,
        string fileName,
        string contentType,
        string storageKey,
        UserId uploadedBy,
        DateTimeOffset uploadedAt)
    {
        Id = id;
        FileName = fileName;
        ContentType = contentType;
        StorageKey = storageKey;
        UploadedBy = uploadedBy;
        UploadedAt = uploadedAt;
    }

    public static RequestAttachment Create(string fileName, string contentType, string storageKey, UserId uploadedBy)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new DomainException("Attachment file name is required.");

        if (string.IsNullOrWhiteSpace(contentType))
            throw new DomainException("Attachment content type is required.");

        if (string.IsNullOrWhiteSpace(storageKey))
            throw new DomainException("Attachment storage key is required.");

        return new RequestAttachment(
            new RequestAttachmentId(Guid.NewGuid()),
            fileName,
            contentType,
            storageKey,
            uploadedBy,
            DateTimeOffset.UtcNow);
    }
}
