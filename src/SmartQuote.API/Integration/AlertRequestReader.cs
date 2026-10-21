using SmartQuote.API.SupplyRequests.Application.OutboundServices;
using SmartQuote.API.PurchaseOrdering.Application.Ports;
using SmartQuote.Modules.QuotationIntake.Application.Ports;

namespace SmartQuote.API.Integration;

public sealed class AlertRequestReader(IPurchaseRequestSnapshotProvider requests, IOrderingPolicyStore ordering) : IAlertRequestReader
{
    public async Task<AlertRequestState?> ReadAsync(Guid requestId, CancellationToken ct)
    {
        var r = await requests.GetSnapshotAsync(requestId, ct);
        return r is null ? null : new(r.Status is not ("Draft" or "Ordered" or "Rejected" or "Cancelled"),
            await ordering.HasOrderAsync(requestId, ct), r.AssignedAnalystId);
    }
}
