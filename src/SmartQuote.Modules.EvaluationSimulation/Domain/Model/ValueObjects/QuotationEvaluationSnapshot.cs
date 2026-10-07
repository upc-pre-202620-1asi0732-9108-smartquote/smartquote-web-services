using SmartQuote.API.Shared.Domain.Model.ValueObjects;

namespace SmartQuote.Modules.EvaluationSimulation.Domain.Model.ValueObjects;

public class QuotationEvaluationSnapshot
{
    private readonly List<QuotationLineSnapshotData> _lines = [];

    public string QuotationId { get; private set; } = string.Empty;
    public long Version { get; private set; }
    public string SupplierId { get; private set; } = string.Empty;
    public string SupplierBusinessName { get; private set; } = string.Empty;
    public string SupplierTaxIdentifier { get; private set; } = string.Empty;
    public string Currency { get; private set; } = string.Empty;
    public int DeliveryLeadTimeDays { get; private set; }
    public DateTimeOffset VerifiedAt { get; private set; }
    public DateTimeOffset CapturedAt { get; private set; }

    public IReadOnlyList<QuotationLineSnapshotData> Lines => _lines.AsReadOnly();

    private QuotationEvaluationSnapshot() { }

    public QuotationEvaluationSnapshot(
        string quotationId,
        long version,
        string supplierId,
        string supplierBusinessName,
        string supplierTaxIdentifier,
        string currency,
        int deliveryLeadTimeDays,
        DateTimeOffset verifiedAt,
        IEnumerable<QuotationLineSnapshotData> lines,
        DateTimeOffset capturedAt)
    {
        QuotationId = quotationId;
        Version = version;
        SupplierId = supplierId;
        SupplierBusinessName = supplierBusinessName;
        SupplierTaxIdentifier = supplierTaxIdentifier;
        Currency = currency;
        DeliveryLeadTimeDays = deliveryLeadTimeDays;
        VerifiedAt = verifiedAt;
        CapturedAt = capturedAt;
        _lines.AddRange(lines);
    }

    public Money TotalPrice() =>
        Lines.Aggregate(new Money(0, Currency), (total, line) => total + new Money(line.Quantity * line.UnitPrice, Currency));

    public IEnumerable<QuotationSpecificationSnapshotData> AllSpecifications() => Lines.SelectMany(line => line.Specifications);
}

public class QuotationLineSnapshotData
{
    private readonly List<QuotationSpecificationSnapshotData> _specifications = [];

    public string? SourceQuotationLineId { get; private set; }
    public string? SourceRequestedItemId { get; private set; }
    public int LineNumber { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public decimal Quantity { get; private set; }
    public string UnitOfMeasure { get; private set; } = string.Empty;
    public decimal UnitPrice { get; private set; }

    public IReadOnlyList<QuotationSpecificationSnapshotData> Specifications => _specifications.AsReadOnly();

    private QuotationLineSnapshotData() { }

    public QuotationLineSnapshotData(
        string? sourceQuotationLineId,
        string? sourceRequestedItemId,
        int lineNumber,
        string description,
        decimal quantity,
        string unitOfMeasure,
        decimal unitPrice,
        IEnumerable<QuotationSpecificationSnapshotData> specifications)
    {
        SourceQuotationLineId = sourceQuotationLineId;
        SourceRequestedItemId = sourceRequestedItemId;
        LineNumber = lineNumber;
        Description = description;
        Quantity = quantity;
        UnitOfMeasure = unitOfMeasure;
        UnitPrice = unitPrice;
        _specifications.AddRange(specifications);
    }
}

public record QuotationSpecificationSnapshotData(string Name, string Value, string UnitOfMeasure);
