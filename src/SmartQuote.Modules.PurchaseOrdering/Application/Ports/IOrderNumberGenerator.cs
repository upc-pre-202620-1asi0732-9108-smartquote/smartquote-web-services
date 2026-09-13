using SmartQuote.API.PurchaseOrdering.Domain.Model.ValueObjects;

namespace SmartQuote.API.PurchaseOrdering.Application.Ports;

public interface IOrderNumberGenerator
{
    Task<OrderNumber> NextAsync(CancellationToken cancellationToken = default);
}
