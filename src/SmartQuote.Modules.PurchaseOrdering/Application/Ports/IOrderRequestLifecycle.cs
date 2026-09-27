namespace SmartQuote.API.PurchaseOrdering.Application.Ports;

public interface IOrderRequestLifecycle
{
    Task EnsureOrderedAsync(string requestId, string orderNumber, CancellationToken cancellationToken = default);
}
