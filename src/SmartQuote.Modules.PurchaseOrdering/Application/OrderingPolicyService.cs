using SmartQuote.API.PurchaseOrdering.Application.Ports;
using SmartQuote.API.PurchaseOrdering.Domain.Model.Aggregates;
using SmartQuote.API.PurchaseOrdering.Domain.Model.ValueObjects;
using SmartQuote.API.Shared.Application.Security;
using SmartQuote.API.Shared.Domain;
using SmartQuote.Modules.EvaluationSimulation.Application.OutboundServices;
using SmartQuote.Modules.PurchaseOrdering.Application.OutboundServices;

namespace SmartQuote.Modules.PurchaseOrdering.Application;

public sealed record PurchaseBudgetView(Guid RequestId, string Status, decimal? AmountPen, long? Version,
    IReadOnlyList<PurchaseBudgetRevision> Revisions);
public sealed record BudgetCheckView(string SimulationRunId, string QuotationId, string InputFingerprint,
    BudgetAssessment Assessment, ApprovedExchangeRateSnapshot? ExchangeRate);

public sealed class SupplierDirectoryService(IOrderingPolicyStore store, TimeProvider clock) : ISupplierDirectory
{
    public async Task<Guid> RegisterIdentifiedAsync(string taxIdentifier, string businessName, CancellationToken ct = default)
    {
        var tax = SupplierAccount.NormalizeTaxIdentifier(taxIdentifier);
        await using var lease = await store.LockAsync(null, tax, ct);
        var supplier = await store.SupplierByTaxAsync(tax, ct);
        if (supplier is null)
        {
            supplier = SupplierAccount.Register(tax, businessName, clock.GetUtcNow());
            store.AddSupplier(supplier);
        }
        await lease.CommitAsync(ct);
        return supplier.Id;
    }
    public async Task<IReadOnlyList<SupplierEligibilitySnapshot>> GetEligibilityAsync(IReadOnlyList<string> identifiers, CancellationToken ct = default) =>
        (await store.SuppliersByTaxAsync(identifiers, ct)).Select(s => new SupplierEligibilitySnapshot(s.Id, s.TaxIdentifier, s.Status, s.Version)).ToList();
}

public sealed class OrderingPolicyService(IOrderingPolicyStore store, IOrderingRequestReader requests,
    ISimulationDecisionReader decisions, ICurrentUser actor, TimeProvider clock)
{
    public Task<IReadOnlyList<SupplierAccount>> ListSuppliersAsync(string? status, int page, int pageSize, CancellationToken ct)
    {
        if (page < 1 || pageSize is < 1 or > 100) throw new ArgumentException("Invalid pagination.");
        if (status is not null && status is not ("Pending" or "Enabled" or "Suspended")) throw new ArgumentException("Invalid supplier status.");
        return store.ListSuppliersAsync(status, page, pageSize, ct);
    }
    public async Task<SupplierAccount> GetSupplierAsync(Guid id, CancellationToken ct) =>
        await store.SupplierByIdAsync(id, ct) ?? throw new NotFoundException("Supplier was not found.");
    public async Task<SupplierAccount> DecideSupplierAsync(Guid id, string action, string reason, long expectedVersion, CancellationToken ct)
    {
        RequireManager();
        // Resolve identity only before taking the lock. Read the mutable status again under it.
        var identity = await GetSupplierAsync(id, ct);
        await using var lease = await store.LockAsync(null, identity.TaxIdentifier, ct);
        var supplier = await store.SupplierByTaxAsync(identity.TaxIdentifier, ct) ?? throw new NotFoundException("Supplier was not found.");
        CheckVersion(supplier.Version, expectedVersion);
        supplier.Decide(action, reason, actor.UserId, clock.GetUtcNow());
        await lease.CommitAsync(ct);
        return supplier;
    }
    public async Task<PurchaseBudgetView> PutBudgetAsync(Guid requestId, decimal amount, string currency, string reason, long expectedVersion, CancellationToken ct)
    {
        RequireManager();
        await using var lease = await store.LockAsync(requestId, null, ct);
        if (!await requests.IsActiveAsync(requestId, ct) || await store.HasOrderAsync(requestId, ct))
            throw new DomainException("Only an active, unawarded request can receive a new budget authorization.");
        var budget = await store.BudgetAsync(requestId, ct);
        CheckVersion(budget?.Version ?? 0, expectedVersion);
        if (budget is null)
        {
            budget = PurchaseBudget.Create(requestId, amount, currency, reason, actor.UserId, clock.GetUtcNow());
            store.AddBudget(budget);
        }
        else budget.Revise(amount, currency, reason, actor.UserId, clock.GetUtcNow());
        await lease.CommitAsync(ct);
        return BudgetView(requestId, budget);
    }
    public async Task<PurchaseBudgetView> GetBudgetAsync(Guid requestId, CancellationToken ct)
    {
        await requests.IsActiveAsync(requestId, ct);
        return BudgetView(requestId, await store.BudgetAsync(requestId, ct));
    }
    private static PurchaseBudgetView BudgetView(Guid id, PurchaseBudget? budget) => new(id, budget is null ? "NotConfigured" : "Configured",
        budget?.AmountPen, budget?.Version, budget?.Revisions ?? []);

    public async Task<BudgetCheckView> CheckBudgetAsync(Guid runId, string quotationId, CancellationToken ct)
    {
        var snapshot = await decisions.GetApprovedSnapshotAsync(runId, quotationId, ct) ?? throw new NotFoundException("Simulation decision was not found.");
        return new(snapshot.SimulationRunId, quotationId, snapshot.InputFingerprint,
            Assess(snapshot, await store.BudgetAsync(Guid.Parse(snapshot.RequestId), ct)), snapshot.ExchangeRate);
    }
    public static BudgetAssessment Assess(ApprovedSimulationSnapshot snapshot, PurchaseBudget? budget)
    {
        var originalTotal = snapshot.OrderLines.Sum(l => l.Quantity * l.UnitPrice);
        decimal totalPen;
        if (snapshot.Currency == "PEN") totalPen = originalTotal;
        else if (snapshot.Currency == "USD" && snapshot.ExchangeRate is { Rate: > 0, SourceCurrency: "USD", TargetCurrency: "PEN" } rate &&
            !string.IsNullOrWhiteSpace(rate.Source) && !string.IsNullOrWhiteSpace(rate.RateType) && rate.PublishedOn != default && rate.RetrievedAt != default)
            totalPen = decimal.Round(originalTotal * rate.Rate, 4, MidpointRounding.AwayFromZero);
        else throw new DomainException("The acquisition cannot be checked in PEN: a valid exchange rate preserved by the simulation is required.");
        if (snapshot.ComparableTotalPen is not decimal simulated || simulated != totalPen)
            throw new DomainException("The complete order amount does not match the amount evaluated by the simulation. Run a new simulation.");
        return BudgetAssessment.Evaluate(budget?.Version, budget?.AmountPen, [totalPen]);
    }
    public static void CheckVersion(long actual, long expected)
    {
        if (expected < 0) throw new ArgumentException("Expected version cannot be negative.");
        if (actual != expected) throw new ConflictException($"Expected version {expected}, current version is {actual}.");
    }
    private void RequireManager()
    {
        if (!actor.IsInRole(SmartQuoteRoles.PurchaseManager)) throw new UnauthorizedAccessException();
    }
}
