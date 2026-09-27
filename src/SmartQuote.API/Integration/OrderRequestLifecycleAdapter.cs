using SmartQuote.API.PurchaseOrdering.Application.Ports;
using SmartQuote.API.Shared.Domain;
using SmartQuote.API.SupplyRequests.Application;
using SmartQuote.API.SupplyRequests.Application.OutboundServices;
using SmartQuote.API.SupplyRequests.Domain.Model.Commands;
using SmartQuote.API.SupplyRequests.Domain.Model.Enums;

namespace SmartQuote.API.Integration;

// Cross-context orchestration belongs to the composition root, not either domain model.
public sealed class OrderRequestLifecycleAdapter(
    IPurchaseRequestSnapshotProvider snapshots,
    PurchaseRequestCommandService commands) : IOrderRequestLifecycle
{
    public async Task EnsureOrderedAsync(string requestId, string orderNumber, CancellationToken cancellationToken = default)
    {
        var id = Guid.Parse(requestId);
        var snapshot = await snapshots.GetSnapshotAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Purchase request '{requestId}' was not found.");

        if (snapshot.Status == nameof(RequestStatus.Ordered)) return;
        if (snapshot.Status == nameof(RequestStatus.Evaluation))
        {
            await commands.ChangeStatusAsync(new ChangeRequestStatusCommand(
                id, RequestStatus.Approved, $"Purchase order {orderNumber} approved.", snapshot.Version), cancellationToken);
            snapshot = await snapshots.GetSnapshotAsync(id, cancellationToken)
                ?? throw new NotFoundException($"Purchase request '{requestId}' was not found.");
        }

        if (snapshot.Status != nameof(RequestStatus.Approved))
            throw new DomainException($"Purchase request '{requestId}' cannot be marked ordered from '{snapshot.Status}'.");

        await commands.ChangeStatusAsync(new ChangeRequestStatusCommand(
            id, RequestStatus.Ordered, $"Purchase order {orderNumber} issued.", snapshot.Version), cancellationToken);
    }
}
