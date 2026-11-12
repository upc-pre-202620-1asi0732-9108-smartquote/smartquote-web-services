using SmartQuote.API.Shared.Domain;

namespace SmartQuote.API.PurchaseOrdering.Domain.Model.Aggregates;

public record AwardAllocation(Guid RequestedItemId, Guid SupplierId, Guid QuotationId, Guid QuotationLineId,
    decimal Quantity, string UnitOfMeasure, string OriginalCurrency, decimal OriginalUnitPrice, decimal UnitPricePen, decimal AmountPen);
public sealed class PurchaseAward
{
    public Guid Id { get; private set; }
    public Guid RequestId { get; private set; }
    public Guid SimulationRunId { get; private set; }
    public string Mode { get; private set; } = "";
    public string InputFingerprint { get; private set; } = "";
    public string IdempotencyKey { get; private set; } = "";
    public string RequestHash { get; private set; } = "";
    public Guid ApprovedBy { get; private set; }
    public DateTimeOffset ApprovedAt { get; private set; }
    public decimal TotalPen { get; private set; }
    public long? BudgetVersion { get; private set; }
    public decimal? BudgetAmountPen { get; private set; }
    public decimal? BudgetMarginPen { get; private set; }
    public decimal? ExchangeRate { get; private set; }
    public string? ExchangeRateSource { get; private set; }
    public string? ExchangeRateType { get; private set; }
    public DateOnly? RatePublishedOn { get; private set; }
    public DateTimeOffset? RateRetrievedAt { get; private set; }
    public IReadOnlyList<AwardAllocation> Allocations { get; private set; } = new List<AwardAllocation>();
    private PurchaseAward() { }
    public static PurchaseAward Create(Guid requestId, Guid runId, string mode, string fingerprint, string key, string requestHash,
        Guid actor, DateTimeOffset now, decimal totalPen, long? budgetVersion, decimal? budgetAmount, IReadOnlyList<AwardAllocation> allocations)
    {
        if (requestId == Guid.Empty || runId == Guid.Empty || string.IsNullOrWhiteSpace(key) || key.Length > 100 ||
            mode is not ("SingleSupplier" or "DistributedByItem") || totalPen < 0 || (budgetAmount.HasValue && totalPen > budgetAmount) ||
            (mode == "DistributedByItem" && (allocations.Count < 2 || allocations.Select(a => a.RequestedItemId).Distinct().Count() != allocations.Count ||
                allocations.Any(a => a.Quantity <= 0 || a.AmountPen != decimal.Round(a.Quantity * a.UnitPricePen, 4, MidpointRounding.AwayFromZero)) || allocations.Sum(a => a.AmountPen) != totalPen)))
            throw new DomainException("The award must cover each item exactly once, preserve its complete total and respect the authorized budget.");
        return new() { Id = Guid.NewGuid(), RequestId = requestId, SimulationRunId = runId, Mode = mode, InputFingerprint = fingerprint,
            IdempotencyKey = key, RequestHash = requestHash, ApprovedBy = actor, ApprovedAt = now, TotalPen = totalPen,
            BudgetVersion = budgetVersion, BudgetAmountPen = budgetAmount, BudgetMarginPen = budgetAmount - totalPen, Allocations = allocations.ToList() };
    }
    public void RecordRate(decimal rate, string source, string type, DateOnly published, DateTimeOffset retrieved)
    {
        if (rate <= 0 || ExchangeRate.HasValue) throw new DomainException("The award conversion rate must be valid and immutable.");
        ExchangeRate = rate; ExchangeRateSource = source; ExchangeRateType = type; RatePublishedOn = published; RateRetrievedAt = retrieved;
    }
}
