using SmartQuote.API.Shared.Domain;

namespace SmartQuote.API.PurchaseOrdering.Domain.Model.Aggregates;

public sealed record PurchaseBudgetRevision(Guid Id, long Version, decimal AmountPen,
    Guid AuthorizedBy, DateTimeOffset AuthorizedAt, string Reason);

public sealed class PurchaseBudget
{
    private readonly List<PurchaseBudgetRevision> _revisions = [];
    public Guid RequestId { get; private set; }
    public decimal AmountPen { get; private set; }
    public long Version { get; private set; }
    public IReadOnlyList<PurchaseBudgetRevision> Revisions => _revisions.AsReadOnly();
    private PurchaseBudget() { }
    public static PurchaseBudget Create(Guid requestId, decimal amount, string currency, string reason, Guid actorId, DateTimeOffset now)
    {
        if (requestId == Guid.Empty) throw new DomainException("The purchase request is required.");
        var budget = new PurchaseBudget { RequestId = requestId };
        budget.Revise(amount, currency, reason, actorId, now);
        return budget;
    }
    public void Revise(decimal amount, string currency, string reason, Guid actorId, DateTimeOffset now)
    {
        if (amount <= 0 || amount >= 100000000000000m || decimal.Round(amount, 4) != amount)
            throw new DomainException("Budget must be positive with at most four decimal places.");
        if (!string.Equals(currency, "PEN", StringComparison.OrdinalIgnoreCase))
            throw new DomainException("An acquisition budget must be authorized in PEN.");
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 2000)
            throw new DomainException("A budget justification of 1–2000 characters is required.");
        AmountPen = amount;
        Version++;
        _revisions.Add(new(Guid.NewGuid(), Version, amount, actorId, now, reason.Trim()));
    }
}
