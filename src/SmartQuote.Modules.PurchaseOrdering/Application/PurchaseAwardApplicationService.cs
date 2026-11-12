using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SmartQuote.API.PurchaseOrdering.Application.Ports;
using SmartQuote.API.PurchaseOrdering.Application.Views;
using SmartQuote.API.PurchaseOrdering.Domain.Model.Aggregates;
using SmartQuote.API.PurchaseOrdering.Domain.Model.ValueObjects;
using SmartQuote.API.Shared.Application.Security;
using SmartQuote.API.Shared.Domain;
using SmartQuote.API.Shared.Domain.Model.ValueObjects;
using SmartQuote.Modules.EvaluationSimulation.Application.OutboundServices;

namespace SmartQuote.Modules.PurchaseOrdering.Application;

public record ApproveAwardCommand(string DeliveryConditions, string DeliveryDestination, long? ExpectedBudgetVersion = null, string? ExpectedInputFingerprint = null);
public record PurchaseAwardView(PurchaseAward Award, IReadOnlyList<PurchaseOrderView> Orders);
public record PurchaseAwardExecution(PurchaseAwardView Result, bool WasCreated);
public sealed class PurchaseAwardApplicationService(IDistributedDecisionReader decisions, IPurchaseAwardStore awards,
    IOrderingPolicyStore policies, IPurchaseOrderRepository orders, IOrderNumberGenerator numbers,
    IAtomicAwardCommitter committer, ICurrentUser user, TimeProvider clock)
{
    public async Task<PurchaseAwardExecution> ApproveAsync(Guid runId, string key, ApproveAwardCommand command, CancellationToken ct)
    {
        if (!user.IsInRole(SmartQuoteRoles.PurchaseManager)) throw new UnauthorizedAccessException();
        if (string.IsNullOrWhiteSpace(key) || key.Length > 100) throw new ArgumentException("Provide an Idempotency-Key of 1 to 100 characters.");
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { runId, command }))));
        var requestId = Guid.Parse(await decisions.RequestIdAsync(runId, ct));
        await using var lease = await policies.LockAsync(requestId, null, ct);
        var existing = await awards.ByKeyAsync(key, ct);
        if (existing is not null)
        {
            if (existing.RequestHash != hash) throw new ConflictException("This idempotency key was already used for different approval content.");
            await lease.CommitAsync(ct); return new(await ToView(existing, ct), false);
        }
        if (await awards.ByRequestAsync(requestId, ct) is not null || await policies.HasOrderAsync(requestId, ct))
            throw new ConflictException("The purchase request already has an award.");
        var snapshot = await decisions.ReadCurrentAsync(runId, ct);
        var allocations = snapshot.Allocations;
        await awards.LockSuppliersAsync(allocations.Select(a => a.SupplierTaxIdentifier).ToList(), ct);
        // Re-read versions after all supplier locks; a suspension during lock acquisition invalidates the run.
        snapshot = await decisions.ReadCurrentAsync(runId, ct); allocations = snapshot.Allocations;
        foreach (var a in allocations)
        {
            var supplier = await policies.SupplierByTaxAsync(a.SupplierTaxIdentifier, ct);
            if (supplier is null || supplier.Status != "Enabled" || supplier.Id != a.SupplierAccountId || supplier.Version != a.SupplierEligibilityVersion)
                throw new ConflictException("Supplier eligibility changed; repeat the simulation.");
        }
        if (command.ExpectedInputFingerprint is not null && command.ExpectedInputFingerprint != snapshot.InputFingerprint)
            throw new ConflictException("The decision fingerprint changed.");
        var budget = await policies.BudgetAsync(requestId, ct);
        if (command.ExpectedBudgetVersion.HasValue && command.ExpectedBudgetVersion != (budget?.Version ?? 0))
            throw new ConflictException("The budget changed. Refresh the check before approval.", "budget_check_outdated");
        var assessment = BudgetAssessment.Evaluate(budget?.Version, budget?.AmountPen, allocations.Select(a => a.AmountPen).ToList());
        if (!assessment.CanApprove) throw new DomainException($"The complete acquisition exceeds the budget by {assessment.ExcessPen} PEN.");
        var award = PurchaseAward.Create(requestId, runId, "DistributedByItem", snapshot.InputFingerprint, key, hash, user.UserId, clock.GetUtcNow(),
            assessment.TotalPen, budget?.Version, budget?.AmountPen, allocations.Select(a => new AwardAllocation(Guid.Parse(a.RequestedItemId),
                a.SupplierAccountId, Guid.Parse(a.QuotationId), Guid.Parse(a.QuotationLineId), a.Quantity, a.UnitOfMeasure,
                a.OriginalCurrency, a.OriginalUnitPrice, a.UnitPricePen, a.AmountPen)).ToList());
        if (snapshot.ExchangeRate is { } rate) award.RecordRate(rate.Rate, rate.Source, rate.RateType, rate.PublishedOn, rate.RetrievedAt);
        awards.Add(award); var created = new List<PurchaseOrder>();
        foreach (var group in allocations.GroupBy(a => a.SupplierAccountId).OrderBy(g => g.Key))
        {
            var supplier = group.First();
            var decision = new ApprovedPurchaseDecision(runId.ToString(), requestId.ToString(), null,
                new(group.Key.ToString(), supplier.SupplierBusinessName, supplier.SupplierTaxIdentifier), "PEN",
                group.Select(a => new ApprovedPurchaseLine(a.QuotationLineId, a.RequestedItemId, a.Description, a.Quantity,
                    a.UnitOfMeasure, a.UnitPricePen, a.QuotationId, a.OriginalCurrency, a.OriginalUnitPrice)).ToList(),
                DeliveryTerms.Create(group.Max(a => a.DeliveryLeadTimeDays), command.DeliveryConditions, command.DeliveryDestination), snapshot.InputFingerprint);
            var order = PurchaseOrder.Create(await numbers.NextAsync(ct), decision,
                new(new UserId(user.UserId), award.ApprovedAt, $"{award.Id:N}:{group.Key:N}"));
            order.LinkAward(award.Id, group.Key);
            // Evidence refers to the global commitment, not a misleading per-supplier budget allowance.
            order.RecordPolicyEvidence(new(group.Key, supplier.SupplierEligibilityVersion, assessment.BudgetVersion, assessment.BudgetAmountPen,
                assessment.TotalPen, assessment.MarginPen, snapshot.ExchangeRate?.Rate, snapshot.ExchangeRate?.Source, snapshot.ExchangeRate?.RateType,
                snapshot.ExchangeRate?.PublishedOn, snapshot.ExchangeRate?.RetrievedAt));
            await orders.AddAsync(order, ct); created.Add(order);
        }
        await committer.PrepareAsync(requestId.ToString(), award.Id.ToString(), created.SelectMany(o => o.DomainEvents).ToList(),
            async token => { await decisions.ReadCurrentAsync(runId, token); }, ct);
        await lease.CommitAsync(ct); foreach (var o in created) o.ClearDomainEvents();
        await committer.AfterCommitAsync(ct);
        return new(await ToView(award, ct), true);
    }
    public async Task<PurchaseAwardView> GetAsync(Guid id, CancellationToken ct) => await ToView(await awards.ByIdAsync(id, ct) ?? throw new NotFoundException("Purchase award was not found."), ct);
    public async Task<PurchaseAwardView> GetForRequestAsync(Guid id, CancellationToken ct) => await ToView(await awards.ByRequestAsync(id, ct) ?? throw new NotFoundException("Purchase award was not found."), ct);
    public async Task<object> CheckBudgetAsync(Guid runId, CancellationToken ct)
    {
        var snapshot = await decisions.ReadCurrentAsync(runId, ct);
        var budget = await policies.BudgetAsync(Guid.Parse(snapshot.RequestId), ct);
        return new { SimulationRunId = runId, Mode = "DistributedByItem", snapshot.InputFingerprint,
            Assessment = BudgetAssessment.Evaluate(budget?.Version, budget?.AmountPen, snapshot.Allocations.Select(a => a.AmountPen).ToList()), snapshot.ExchangeRate };
    }
    private async Task<PurchaseAwardView> ToView(PurchaseAward award, CancellationToken ct) => new(award, (await awards.OrdersAsync(award.Id, ct)).Select(PurchaseOrderApplicationService.ToView).ToList());
}
