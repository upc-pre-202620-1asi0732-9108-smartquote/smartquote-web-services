using SmartQuote.API.Shared.Domain;
using SmartQuote.API.Shared.Domain.Model.ValueObjects;
using SmartQuote.API.PurchaseOrdering.Domain.Model.Entities;
using SmartQuote.API.PurchaseOrdering.Domain.Model.Enums;
using SmartQuote.API.PurchaseOrdering.Domain.Model.Events;
using SmartQuote.API.PurchaseOrdering.Domain.Model.ValueObjects;

namespace SmartQuote.API.PurchaseOrdering.Domain.Model.Aggregates;

public class PurchaseOrder : AggregateRoot<PurchaseOrderId>
{
    private readonly List<PurchaseOrderLine> _lines = [];

    public OrderNumber OrderNumber { get; private set; } = null!;
    public SourceSimulationReference SourceDecision { get; private set; } = null!;
    public SupplierSnapshot Supplier { get; private set; } = null!;
    public Approval Approval { get; private set; } = null!;
    public PurchaseOrderStatus Status { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public DeliveryTerms DeliveryTerms { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyList<PurchaseOrderLine> Lines => _lines.AsReadOnly();

    private PurchaseOrder() { }

    public static PurchaseOrder Create(OrderNumber number, ApprovedPurchaseDecision decision, Approval approval)
    {
        var order = new PurchaseOrder
        {
            Id = new PurchaseOrderId(Guid.NewGuid()),
            OrderNumber = number,
            SourceDecision = new SourceSimulationReference(decision.SimulationRunId, decision.PurchaseRequestId, decision.QuotationId, decision.InputFingerprint),
            Supplier = decision.Supplier,
            Approval = approval,
            Status = PurchaseOrderStatus.Issued,
            Currency = decision.Currency,
            DeliveryTerms = decision.DeliveryTerms,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var lineNumber = 1;
        foreach (var line in decision.Lines)
            order.AddLine(PurchaseOrderLine.Create(lineNumber++, line));

        order.Issue();

        return order;
    }

    public void AddLine(PurchaseOrderLine line) => _lines.Add(line);

    public void Issue()
    {
        if (_lines.Count == 0)
            throw new DomainException("A purchase order must contain at least one line.");

        AddDomainEvent(new PurchaseOrderIssued(Id, SourceDecision.SimulationRunId, SourceDecision.PurchaseRequestId, DateTimeOffset.UtcNow));
    }

    public Money CalculateTotal() =>
        _lines.Aggregate(new Money(0, Currency), (total, line) => total + line.CalculateSubtotal(Currency));
}
