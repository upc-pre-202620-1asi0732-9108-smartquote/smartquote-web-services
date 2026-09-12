using SmartQuote.API.Shared.Domain;
using SmartQuote.API.Shared.Domain.Model.ValueObjects;
using SmartQuote.Modules.QuotationIntake.Domain.Model.ValueObjects;

namespace SmartQuote.Modules.QuotationIntake.Domain.Model.Entities;

public class QuotationLine
{
    private readonly List<QuotedSpecification> _specifications = [];

    public QuotationLineId Id { get; private set; } = null!;
    public int LineNumber { get; private set; }
    public string? Description { get; private set; }
    public decimal? Quantity { get; private set; }
    public string? UnitOfMeasure { get; private set; }

    public decimal? UnitPrice { get; private set; }

    public string? RequestedItemId { get; private set; }

    public IReadOnlyList<QuotedSpecification> Specifications => _specifications.AsReadOnly();

    private QuotationLine() { }

    private QuotationLine(QuotationLineId id, int lineNumber, string? description, decimal? quantity, string? unitOfMeasure, decimal? unitPrice)
    {
        Id = id;
        LineNumber = lineNumber;
        Description = description;
        Quantity = quantity;
        UnitOfMeasure = unitOfMeasure;
        UnitPrice = unitPrice;
    }

    public static QuotationLine Create(int lineNumber, string? description, decimal? quantity, string? unitOfMeasure, decimal? unitPrice)
    {
        if (lineNumber <= 0)
            throw new DomainException("Line number must be greater than zero.");

        if (quantity.HasValue && quantity <= 0)
            throw new DomainException("Quotation line quantity must be greater than zero when resolved.");

        if (unitPrice.HasValue && unitPrice < 0)
            throw new DomainException("Quotation line unit price cannot be negative.");

        return new QuotationLine(new QuotationLineId(Guid.NewGuid()), lineNumber, description, quantity, unitOfMeasure, unitPrice);
    }

    public Money AsMoney(string currency) => new(UnitPrice ?? throw new DomainException("Quotation line unit price is unresolved."), currency);

    public Money CalculateSubtotal(string currency) => AsMoney(currency) * (Quantity ?? throw new DomainException("Quotation line quantity is unresolved."));

    public void AddSpecification(QuotedSpecification specification) => _specifications.Add(specification);

    public void LinkToRequestedItem(string requestedItemId)
    {
        if (!Guid.TryParse(requestedItemId, out _))
            throw new DomainException("Requested item reference must be a valid UUID.");

        RequestedItemId = requestedItemId;
    }

    public bool IsComplete() =>
        !string.IsNullOrWhiteSpace(Description) &&
        Quantity is > 0 &&
        !string.IsNullOrWhiteSpace(UnitOfMeasure) &&
        UnitPrice is >= 0;

    public void CorrectDescription(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("Quotation line description is required.");
        Description = value.Trim();
    }

    public void CorrectQuantity(decimal value)
    {
        if (value <= 0)
            throw new DomainException("Quotation line quantity must be greater than zero.");
        Quantity = value;
    }

    public void CorrectUnitOfMeasure(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("Quotation line unit of measure is required.");
        UnitOfMeasure = value.Trim();
    }

    public void CorrectUnitPrice(decimal value)
    {
        if (value < 0)
            throw new DomainException("Quotation line unit price cannot be negative.");
        UnitPrice = value;
    }
}
