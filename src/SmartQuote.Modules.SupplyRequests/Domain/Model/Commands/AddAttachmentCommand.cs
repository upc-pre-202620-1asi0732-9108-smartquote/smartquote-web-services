namespace SmartQuote.API.SupplyRequests.Domain.Model.Commands;

public record AddAttachmentCommand(
    Guid RequestId,
    string FileName,
    string ContentType,
    byte[] Content,
    long ExpectedVersion);
