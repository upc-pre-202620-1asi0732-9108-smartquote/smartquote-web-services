using SmartQuote.API.SupplyRequests.Domain.Model.Commands;
using SmartQuote.API.SupplyRequests.Domain.Model.Enums;
using SmartQuote.API.SupplyRequests.Interfaces.REST.Resources;

namespace SmartQuote.API.SupplyRequests.Interfaces.REST.Transform;

public static class ChangeRequestStatusCommandFromResourceAssembler
{
    public static ChangeRequestStatusCommand ToCommand(Guid requestId, ChangeRequestStatusResource resource) =>
        new(
            requestId,
            EnumResourceParser.Parse<RequestStatus>(resource.NextStatus, nameof(resource.NextStatus)),
            resource.Reason,
            resource.ExpectedVersion);
}
