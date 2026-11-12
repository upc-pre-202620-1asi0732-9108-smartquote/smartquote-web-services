using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartQuote.API.PurchaseOrdering.Application.Ports;
using SmartQuote.API.PurchaseOrdering.Infrastructure.Persistence.EFC.Configuration;
using SmartQuote.Modules.EvaluationSimulation.Infrastructure.Persistence.EFC.Configuration;
using SmartQuote.Modules.EvaluationSimulation.Application;
using SmartQuote.Modules.EvaluationSimulation.Application.Ports;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.ValueObjects;
using Xunit;

namespace SmartQuote.Integration.Tests;

[Collection("API integration"), Trait("Category", "Integration")]
public sealed class OrderingPolicyAcceptanceTests
{
    private static async Task<JsonElement> Supplier(StoryFlow f)
    {
        var suppliers = await StoryFlow.Json(await f.Manager.GetAsync("/api/v1/suppliers?pageSize=100"));
        var supplier = suppliers.EnumerateArray().Single(s => s.GetProperty("taxIdentifier").GetString() == f.Factory.SupplierTaxIdentifier);
        return await StoryFlow.Json(await f.Manager.GetAsync($"/api/v1/suppliers/{supplier.GetProperty("id")}"));
    }
    private static async Task<JsonElement> Decide(StoryFlow f, string action, JsonElement? supplier = null)
    {
        var s = supplier ?? await Supplier(f);
        return await StoryFlow.Json(await f.Manager.PostAsJsonAsync($"/api/v1/suppliers/{s.GetProperty("id")}/{action}", new
            { reason = "Decisión interna justificada para la prueba", expectedVersion = s.GetProperty("version") }));
    }
    private static string QuoteId(JsonElement run) => run.GetProperty("recommendation").ValueKind == JsonValueKind.Null
        ? run.GetProperty("evaluations")[0].GetProperty("quotationId").GetString()! : run.GetProperty("recommendation").GetProperty("quotationId").GetString()!;
    private static Task<HttpResponseMessage> Approve(StoryFlow f, JsonElement run, long? expectedBudgetVersion = null) =>
        f.Manager.PostAsJsonAsync($"/api/v1/simulations/{run.GetProperty("simulationRunId")}/quotations/{QuoteId(run)}/purchase-orders",
            new { deliveryConditions = "Recepción de prueba", deliveryDestination = "Almacén", expectedBudgetVersion });
    private static async Task<JsonElement> Budget(StoryFlow f, decimal amount, long expectedVersion = 0) =>
        await StoryFlow.Json(await f.Manager.PutAsJsonAsync($"/api/v1/purchase-requests/{f.RequestId}/budget", new
            { amount, currency = "PEN", reason = "Presupuesto autorizado por el jefe", expectedVersion }));
    private static async Task<JsonElement> Check(StoryFlow f, JsonElement run) => await StoryFlow.Json(await f.Manager.GetAsync(
        $"/api/v1/simulations/{run.GetProperty("simulationRunId")}/budget-check?quotationId={QuoteId(run)}"));
    private static async Task AssertNoOrders(StoryFlow f)
    {
        using var scope = f.Factory.Services.CreateScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<PurchaseOrderingDbContext>().PurchaseOrders.AnyAsync(o => o.SourceDecision.PurchaseRequestId == f.RequestId.ToString()));
    }

    // US18/E1: extraction/verification never grants purchasing eligibility.
    [Fact, Trait("Story", "US18"), Trait("Scenario", "E1")]
    public async Task SupplierStartsPendingAndOnlyManagerCanEnableWithHistory()
    {
        using var f = new StoryFlow(enableSuppliers: false); await f.Create(true); await f.Quote("normal.pdf");
        var s = await Supplier(f); Assert.Equal("Pending", s.GetProperty("status").GetString());
        using var forbidden = await f.Analyst.PostAsJsonAsync($"/api/v1/suppliers/{s.GetProperty("id")}/enable", new { reason = "Test", expectedVersion = 1 });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        s = await Decide(f, "enable", s); Assert.Equal("Enabled", s.GetProperty("status").GetString());
        var entry = Assert.Single(s.GetProperty("history").EnumerateArray());
        Assert.Equal(f.ManagerId, entry.GetProperty("decidedBy").GetGuid()); Assert.Equal("Pending", entry.GetProperty("previousStatus").GetString());
        Assert.False(string.IsNullOrWhiteSpace(entry.GetProperty("reason").GetString()));
        // Identical RUC in a second PDF uses the same account, without replacing the decision.
        await f.Quote("expensive.pdf"); var same = await Supplier(f); Assert.Equal(s.GetProperty("id").GetGuid(), same.GetProperty("id").GetGuid());
        Assert.Equal(s.GetProperty("version").GetInt64(), same.GetProperty("version").GetInt64());
    }

    [Fact, Trait("Story", "US18"), Trait("Scenario", "E2")]
    public async Task SuspensionInvalidatesPendingRecommendationButPreservesIssuedOrder()
    {
        using var f = new StoryFlow(); var (_, firstRun) = await f.Ready(); var order = await StoryFlow.Json(await Approve(f, firstRun), HttpStatusCode.Created);
        var (_, pendingRun) = await f.Ready(); await Decide(f, "suspend");
        var result = await StoryFlow.Json(await f.Manager.GetAsync($"/api/v1/simulations/{pendingRun.GetProperty("simulationRunId")}"));
        Assert.False(result.GetProperty("isCurrent").GetBoolean());
        Assert.Equal(pendingRun.GetProperty("inputFingerprint").GetString(), result.GetProperty("inputFingerprint").GetString());
        var existing = await StoryFlow.Json(await f.Manager.GetAsync($"/api/v1/purchase-orders/{order.GetProperty("purchaseOrderId")}"));
        UserStoryAcceptanceTests.AssertEquivalentJson(order, existing);
        var retry = await StoryFlow.Json(await Approve(f, firstRun)); Assert.Equal(order.GetProperty("purchaseOrderId").GetGuid(), retry.GetProperty("purchaseOrderId").GetGuid());
    }

    [Theory, Trait("Story", "US18"), Trait("Scenario", "E3")]
    [InlineData(false), InlineData(true)]
    public async Task PendingOrSuspendedSupplierCannotReceiveNewOrder(bool suspended)
    {
        using var f = new StoryFlow(enableSuppliers: suspended); var (_, run) = await f.Ready();
        if (suspended) await Decide(f, "suspend");
        else
        {
            Assert.Equal(JsonValueKind.Null, run.GetProperty("recommendation").ValueKind);
            Assert.All(run.GetProperty("evaluations").EnumerateArray(), e => Assert.False(e.GetProperty("isEligible").GetBoolean()));
        }
        using var result = await Approve(f, run); Assert.Equal(HttpStatusCode.UnprocessableEntity, result.StatusCode); await AssertNoOrders(f);
    }

    [Fact, Trait("Story", "US18"), Trait("Scenario", "E4")]
    public async Task ReactivationRequiresNewEvaluationAndDoesNotReviveOldRun()
    {
        using var f = new StoryFlow(); var (scenario, run) = await f.Ready();
        await Decide(f, "suspend"); var reactivated = await Decide(f, "reactivate");
        Assert.Equal(3, reactivated.GetProperty("history").GetArrayLength());
        var old = await StoryFlow.Json(await f.Manager.GetAsync($"/api/v1/simulations/{run.GetProperty("simulationRunId")}")); Assert.False(old.GetProperty("isCurrent").GetBoolean());
        using var rejected = await Approve(f, run); Assert.Equal(HttpStatusCode.UnprocessableEntity, rejected.StatusCode);
        var updated = await f.Simulate(scenario); Assert.NotEqual(run.GetProperty("simulationRunId").GetGuid(), updated.GetProperty("simulationRunId").GetGuid());
        using var approved = await Approve(f, updated); Assert.Equal(HttpStatusCode.Created, approved.StatusCode);
    }

    // US18/E3: an approval waiting behind an uncommitted suspension must observe its final state.
    [Fact, Trait("Story", "US18"), Trait("Scenario", "E3")]
    public async Task ConcurrentSuspensionCannotBeBypassedByApproval()
    {
        using var f = new StoryFlow(); var (_, run) = await f.Ready(); Task<HttpResponseMessage> approval;
        using (var scope = f.Factory.Services.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IOrderingPolicyStore>();
            await using var lease = await store.LockAsync(null, f.Factory.SupplierTaxIdentifier, default);
            var supplier = (await store.SupplierByTaxAsync(f.Factory.SupplierTaxIdentifier, default))!;
            supplier.Decide("suspend", "Suspensión concurrente", f.ManagerId, DateTimeOffset.UtcNow);
            approval = Approve(f, run);
            await Task.Delay(100); Assert.False(approval.IsCompleted);
            await lease.CommitAsync(default);
        }
        using var result = await approval; Assert.Equal(HttpStatusCode.UnprocessableEntity, result.StatusCode); await AssertNoOrders(f);
    }

    [Fact, Trait("Story", "US20"), Trait("Scenario", "E1")]
    public async Task BudgetIsAbsentNotZeroAndOnlyManagerAuthorizesValidPenLimit()
    {
        using var f = new StoryFlow(); await f.Create();
        var missing = await StoryFlow.Json(await f.Manager.GetAsync($"/api/v1/purchase-requests/{f.RequestId}/budget"));
        Assert.Equal("NotConfigured", missing.GetProperty("status").GetString()); Assert.Equal(JsonValueKind.Null, missing.GetProperty("amountPen").ValueKind);
        using var forbidden = await f.Analyst.PutAsJsonAsync($"/api/v1/purchase-requests/{f.RequestId}/budget", new { amount = 5000, currency = "PEN", reason = "Test", expectedVersion = 0 });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        var budget = await Budget(f, 5000); Assert.Equal(1, budget.GetProperty("version").GetInt64());
        Assert.Equal(f.ManagerId, budget.GetProperty("revisions")[0].GetProperty("authorizedBy").GetGuid());
        using var zero = await f.Manager.PutAsJsonAsync($"/api/v1/purchase-requests/{f.RequestId}/budget", new { amount = 0, currency = "PEN", reason = "Test", expectedVersion = 1 });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, zero.StatusCode);
    }

    [Theory, Trait("Story", "US20"), Trait("Scenario", "E2")]
    [InlineData(4000), InlineData(5000)]
    public async Task OrderAtOrBelowBudgetPersistsAuthorizationEvidence(decimal limit)
    {
        using var f = new StoryFlow(); var (_, run) = await f.Ready(); await Budget(f, limit);
        var check = await Check(f, run); Assert.Equal("WithinBudget", check.GetProperty("assessment").GetProperty("status").GetString());
        var order = await StoryFlow.Json(await Approve(f, run, 1), HttpStatusCode.Created);
        Assert.Equal(4000, order.GetProperty("policyEvidence").GetProperty("totalPen").GetDecimal());
        Assert.Equal(limit - 4000, order.GetProperty("policyEvidence").GetProperty("marginPen").GetDecimal());
        Assert.Equal(1, order.GetProperty("policyEvidence").GetProperty("budgetVersion").GetInt64());
    }

    [Fact, Trait("Story", "US20"), Trait("Scenario", "E2")]
    public async Task UsdApprovalReusesRecordedSunatRateEvenWhenSourceIsNowUnavailable()
    {
        using var f = new StoryFlow(); var (_, run) = await f.Ready("usd.pdf", "normal.pdf"); await Budget(f, 3500);
        var calls = f.Factory.ExchangeRate.Calls; f.Factory.ExchangeRate.Unavailable = true;
        var check = await Check(f, run); Assert.Equal(3500, check.GetProperty("assessment").GetProperty("totalPen").GetDecimal());
        var order = await StoryFlow.Json(await Approve(f, run, 1), HttpStatusCode.Created);
        Assert.Equal("USD", order.GetProperty("currency").GetString()); Assert.Equal(1000, order.GetProperty("total").GetDecimal());
        Assert.Equal(3.5m, order.GetProperty("policyEvidence").GetProperty("exchangeRate").GetDecimal()); Assert.Equal(calls, f.Factory.ExchangeRate.Calls);
    }

    [Fact, Trait("Story", "US20"), Trait("Scenario", "E3")]
    public async Task ExceededBudgetCreatesNoOrderEvenWithoutClientVersion()
    {
        using var f = new StoryFlow(); var (_, run) = await f.Ready(); await Budget(f, 3000);
        var check = await Check(f, run); Assert.Equal(1000, check.GetProperty("assessment").GetProperty("excessPen").GetDecimal());
        using var result = await Approve(f, run); Assert.Equal(HttpStatusCode.UnprocessableEntity, result.StatusCode); await AssertNoOrders(f);
    }

    [Fact, Trait("Story", "US20"), Trait("Scenario", "E4")]
    public async Task ChangedBudgetRejectsStaleCheckAndKeepsBothVersions()
    {
        using var f = new StoryFlow(); var (_, run) = await f.Ready(); await Budget(f, 5000); await Check(f, run); await Budget(f, 4500, 1);
        var conflict = await StoryFlow.Json(await Approve(f, run, 1), HttpStatusCode.Conflict);
        Assert.Equal("budget_check_outdated", conflict.GetProperty("code").GetString()); Assert.Equal(2, conflict.GetProperty("currentBudgetVersion").GetInt64()); await AssertNoOrders(f);
        var check = await Check(f, run); Assert.Equal(2, check.GetProperty("assessment").GetProperty("budgetVersion").GetInt64());
        var order = await StoryFlow.Json(await Approve(f, run, 2), HttpStatusCode.Created);
        var history = await StoryFlow.Json(await f.Manager.GetAsync($"/api/v1/purchase-requests/{f.RequestId}/budget")); Assert.Equal(2, history.GetProperty("revisions").GetArrayLength());
        using var lateChange = await f.Manager.PutAsJsonAsync($"/api/v1/purchase-requests/{f.RequestId}/budget", new { amount = 3000, currency = "PEN", reason = "Después de aprobar", expectedVersion = 2 });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, lateChange.StatusCode);
        var stored = await StoryFlow.Json(await f.Manager.GetAsync($"/api/v1/purchase-orders/{order.GetProperty("purchaseOrderId")}")); UserStoryAcceptanceTests.AssertEquivalentJson(order, stored);
    }

    [Fact, Trait("Story", "US20"), Trait("Scenario", "E3"), Trait("Scenario", "E4")]
    public async Task ConcurrentBudgetReductionCannotUsePreviouslySufficientLimit()
    {
        using var f = new StoryFlow(); var (_, run) = await f.Ready(); await Budget(f, 5000); Task<HttpResponseMessage> approval;
        using (var scope = f.Factory.Services.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IOrderingPolicyStore>();
            await using var lease = await store.LockAsync(f.RequestId, null, default);
            var budget = (await store.BudgetAsync(f.RequestId, default))!;
            budget.Revise(3000, "PEN", "Reducción concurrente", f.ManagerId, DateTimeOffset.UtcNow);
            approval = Approve(f, run);
            await Task.Delay(100); Assert.False(approval.IsCompleted);
            await lease.CommitAsync(default);
        }
        using var result = await approval; Assert.Equal(HttpStatusCode.UnprocessableEntity, result.StatusCode); await AssertNoOrders(f);
    }

    [Fact, Trait("Story", "US20"), Trait("Scenario", "E2")]
    public async Task ConcurrentApprovalsReturnOneAuthorizedOrderWithoutDuplicatingCommitment()
    {
        using var f = new StoryFlow(); var (_, run) = await f.Ready(); await Budget(f, 4000);
        var responses = await Task.WhenAll(Approve(f, run, 1), Approve(f, run, 1));
        var first = await StoryFlow.Json(responses[0], HttpStatusCode.Created, HttpStatusCode.OK);
        var second = await StoryFlow.Json(responses[1], HttpStatusCode.Created, HttpStatusCode.OK);
        Assert.Equal(first.GetProperty("purchaseOrderId").GetGuid(), second.GetProperty("purchaseOrderId").GetGuid());
    }

    // US20/E3: simulate a legacy/inconsistent persisted run with no FX evidence, not a network outage after approval.
    [Fact, Trait("Story", "US20"), Trait("Scenario", "E3")]
    public async Task MissingStoredConversionCannotProduceOrder()
    {
        using var f = new StoryFlow(); var (_, run) = await f.Ready("usd.pdf", "normal.pdf"); await Budget(f, 4000);
        var runId = run.GetProperty("simulationRunId").GetGuid();
        using (var scope = f.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EvaluationSimulationDbContext>();
            var entity = await scope.ServiceProvider.GetRequiredService<ISimulationRunRepository>().GetByIdAsync(new(runId));
            var scenario = await scope.ServiceProvider.GetRequiredService<IEvaluationScenarioRepository>().GetByIdAsync(entity!.ScenarioId);
            db.Entry(entity).Reference(r => r.ExchangeRate).CurrentValue = null;
            var dataset = new EvaluationDataset(entity.RequestSnapshot, entity.QuotationSnapshots);
            db.Entry(entity).Property(r => r.InputFingerprint).CurrentValue = InputFingerprint.FromParts(dataset.CalculateFingerprint().Value, scenario!.CalculateDefinitionFingerprint().Value);
            await db.SaveChangesAsync();
        }
        var quoteId = run.GetProperty("evaluations").EnumerateArray().Single(e => e.GetProperty("originalCurrency").GetString() == "USD").GetProperty("quotationId").GetString();
        var error = await StoryFlow.Json(await f.Manager.PostAsJsonAsync($"/api/v1/simulations/{runId}/quotations/{quoteId}/purchase-orders", new
            { deliveryConditions = "Test", deliveryDestination = "Almacén", expectedBudgetVersion = 1 }), HttpStatusCode.UnprocessableEntity);
        Assert.Contains("exchange rate", error.GetProperty("detail").GetString()); await AssertNoOrders(f);
    }
}
