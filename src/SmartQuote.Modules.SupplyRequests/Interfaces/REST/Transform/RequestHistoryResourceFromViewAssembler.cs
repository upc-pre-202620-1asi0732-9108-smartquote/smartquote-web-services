using SmartQuote.API.SupplyRequests.Application.Views;
using SmartQuote.API.SupplyRequests.Interfaces.REST.Resources;

namespace SmartQuote.API.SupplyRequests.Interfaces.REST.Transform;

public static class RequestHistoryResourceFromViewAssembler
{
    public static RequestHistoryResource ToResource(RequestHistoryView view) =>
        new(
            view.RequestId,
            view.Entries
                .Select(entry => new RequestStatusEntryResource(
                    entry.FromStatus,
                    entry.ToStatus,
                    entry.ChangedBy,
                    entry.ChangedAt,
                    entry.Reason))
                .ToList());
}
