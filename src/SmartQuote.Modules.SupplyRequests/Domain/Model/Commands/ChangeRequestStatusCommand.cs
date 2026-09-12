using SmartQuote.API.SupplyRequests.Domain.Model.Enums;

namespace SmartQuote.API.SupplyRequests.Domain.Model.Commands;

public record ChangeRequestStatusCommand(
    Guid RequestId,
    RequestStatus NextStatus,
    string Reason,
    long ExpectedVersion);
