using SmartQuote.API.Shared.Domain;
using SmartQuote.API.Shared.Domain.Model.ValueObjects;
using SmartQuote.API.PurchaseOrdering.Domain.Model.ValueObjects;

namespace SmartQuote.API.PurchaseOrdering.Domain.Model.Entities;

public class PurchaseOrderLine
{
    public PurchaseOrderLineId Id { get; private set; } = null!;
    public int LineNumber { get; private set; }
    public string? SourceQuotationLineId { get; private set; }
    public string? SourceRequestedItemId { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public decimal Quantity { get; private set; }
    public string UnitOfMeasure { get; private set; } = string.Empty;
    public decimal UnitPrice { get; private set; }
    public string? SourceQuotationId { get; private set; }
    public string? OriginalCurrency { get; private set; }
    public decimal? OriginalUnitPrice { get; private set; }

    private PurchaseOrderLine() { }

    private PurchaseOrderLine(
        PurchaseOrderLineId id,
        int lineNumber,
        string? sourceQuotationLineId,
        string? sourceRequestedItemId,
        string description,
        decimal quantity,
        string unitOfMeasure,
        decimal unitPrice)
    {
        Id = id;
        LineNumber = lineNumber;
        SourceQuotationLineId = sourceQuotationLineId;
        SourceRequestedItemId = sourceRequestedItemId;
        Description = description;
        Quantity = quantity;
        UnitOfMeasure = unitOfMeasure;
        UnitPrice = unitPrice;
    }

    public static PurchaseOrderLine Create(int lineNumber, ApprovedPurchaseLine source)
    {
        if (lineNumber <= 0)
            throw new DomainException("Purchase order line number must be greater than zero.");
        if (source.Quantity <= 0)
            throw new DomainException("Purchase order line quantity must be greater than zero.");
        if (source.UnitPrice < 0)
            throw new DomainException("Purchase order line price cannot be negative.");
        if (string.IsNullOrWhiteSpace(source.Description) || string.IsNullOrWhiteSpace(source.UnitOfMeasure))
            throw new DomainException("Purchase order line description and unit are required.");
        if (!Guid.TryParse(source.SourceQuotationLineId, out _) || !Guid.TryParse(source.SourceRequestedItemId, out _))
            throw new DomainException("Purchase order lines require source quotation and requested-item UUIDs.");

        var line = new PurchaseOrderLine(
            new PurchaseOrderLineId(Guid.NewGuid()),
            lineNumber,
            source.SourceQuotationLineId,
            source.SourceRequestedItemId,
            source.Description,
            source.Quantity,
            source.UnitOfMeasure,
            source.UnitPrice);
        line.SourceQuotationId = source.SourceQuotationId;
        line.OriginalCurrency = source.OriginalCurrency;
        line.OriginalUnitPrice = source.OriginalUnitPrice;
        return line;
    }

    public Money CalculateSubtotal(string currency) => new Money(Quantity * UnitPrice, currency);
}
