using Microsoft.EntityFrameworkCore;
using SmartQuote.API.PurchaseOrdering.Application.Ports;
using SmartQuote.API.PurchaseOrdering.Infrastructure.Persistence.EFC.Configuration;
using SmartQuote.API.PurchaseOrdering.Domain.Model.ValueObjects;

namespace SmartQuote.API.PurchaseOrdering.Infrastructure.Persistence.EFC.Repositories;

public class SequentialOrderNumberGenerator(PurchaseOrderingDbContext context) : IOrderNumberGenerator
{
    public async Task<OrderNumber> NextAsync(CancellationToken cancellationToken = default)
    {
        var sequence = await context.Database
            .SqlQuery<long>($"SELECT nextval('purchase_ordering.order_number_seq') AS \"Value\"")
            .SingleAsync(cancellationToken);

        return new OrderNumber($"PO-{sequence:00000}");
    }
}
