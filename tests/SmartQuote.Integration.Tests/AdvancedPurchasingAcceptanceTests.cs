using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using SmartQuote.API.PurchaseOrdering.Infrastructure.Persistence.EFC.Configuration;
using SmartQuote.API.SupplyRequests.Infrastructure.Persistence.EFC.Configuration;
using SmartQuote.Modules.QuotationIntake.Application;
using SmartQuote.Modules.QuotationIntake.Infrastructure.Persistence.EFC.Configuration;
using SmartQuote.Modules.EvaluationSimulation.Infrastructure.Persistence.EFC.Configuration;
using Xunit;

namespace SmartQuote.Integration.Tests;

[Collection("API integration"), Trait("Category", "Integration")]
public sealed class AdvancedPurchasingAcceptanceTests
{
    private static async Task Scan(StoryFlow f)
    {
        await using var scope = f.Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<QuotationExpiryService>().ScanAsync(default);
    }
    private static async Task SetAlertSource(StoryFlow f, Guid quoteId, DateOnly expiry, Guid? recipient = null)
    {
        await using var scope = f.Factory.Services.CreateAsyncScope();
        var supply = scope.ServiceProvider.GetRequiredService<SupplyRequestsDbContext>();
        var actor = recipient ?? f.AnalystId;
        await supply.Database.ExecuteSqlInterpolatedAsync($"UPDATE supply_requests.purchase_requests SET assigned_analyst_id = {actor} WHERE id = {f.RequestId}");
        await scope.ServiceProvider.GetRequiredService<QuotationIntakeDbContext>().Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE quotation_intake.poultry_quotes SET valid_until = {expiry} WHERE id = {quoteId}");
    }
    // US17/E1: a real verified quotation produces a durable alert only for the assigned analyst.
    [Fact, Trait("Story", "US17"), Trait("Scenario", "E1")]
    public async Task ExpiryScanCreatesResponsibleAnalystInboxAndRejectsOtherRoles()
    {
        using var f = new StoryFlow(); await f.Create(true); var quote = await f.Quote("normal.pdf");
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-5));
        await SetAlertSource(f, quote.GetProperty("quotationId").GetGuid(), today.AddDays(2)); await Scan(f);
        var inbox = await StoryFlow.Json(await f.Analyst.GetAsync("/api/v1/quotation-expiry-alerts?unreadOnly=true"));
        var alert = Assert.Single(inbox.EnumerateArray()); Assert.Equal(f.RequestId, alert.GetProperty("requestId").GetGuid());
        Assert.Equal(quote.GetProperty("quotationId").GetGuid(), alert.GetProperty("quotationId").GetGuid());
        using var denied = await f.Production.GetAsync("/api/v1/quotation-expiry-alerts"); Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }
    [Fact, Trait("Story", "US17"), Trait("Scenario", "E2")]
    public async Task ParallelScansAndRepeatedReadingDoNotDuplicateOrResetReadState()
    {
        using var f = new StoryFlow(); await f.Create(true); var quote = await f.Quote("normal.pdf");
        await SetAlertSource(f, quote.GetProperty("quotationId").GetGuid(), DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-5)).AddDays(1));
        await Task.WhenAll(Scan(f), Scan(f));
        var first = Assert.Single((await StoryFlow.Json(await f.Analyst.GetAsync("/api/v1/quotation-expiry-alerts"))).EnumerateArray());
        var id = first.GetProperty("id").GetGuid();
        using var read = await f.Analyst.PutAsync($"/api/v1/quotation-expiry-alerts/{id}/read", null); Assert.Equal(HttpStatusCode.NoContent, read.StatusCode);
        await Scan(f); using var readAgain = await f.Analyst.PutAsync($"/api/v1/quotation-expiry-alerts/{id}/read", null);
        var alerts = await StoryFlow.Json(await f.Analyst.GetAsync("/api/v1/quotation-expiry-alerts?unreadOnly=true")); Assert.Empty(alerts.EnumerateArray());
        var other = Guid.NewGuid(); await SetAlertSource(f, quote.GetProperty("quotationId").GetGuid(), DateOnly.Parse(first.GetProperty("validUntil").GetString()!), other); await Scan(f);
        using var otherClient = f.Client("PurchaseAnalyst", other);
        var transferred = Assert.Single((await StoryFlow.Json(await otherClient.GetAsync("/api/v1/quotation-expiry-alerts"))).EnumerateArray());
        Assert.Equal(id, transferred.GetProperty("id").GetGuid()); Assert.NotEqual(JsonValueKind.Null, transferred.GetProperty("readAt").ValueKind);
    }
    [Fact, Trait("Story", "US17"), Trait("Scenario", "E3")]
    public async Task ExpiryInvalidatesApprovalAndDoesNotCreateNewPreventiveAlert()
    {
        var clock = new WorkflowClock(DateTimeOffset.UtcNow); using var f = new StoryFlow(clock); var (scenario, run) = await f.Ready();
        var quoteId = Guid.Parse(run.GetProperty("recommendation").GetProperty("quotationId").GetString()!);
        var today = DateOnly.FromDateTime(clock.Now.UtcDateTime.AddHours(-5)); await SetAlertSource(f, quoteId, today); await Scan(f);
        clock.Now = clock.Now.AddDays(1); await Scan(f);
        var current = await StoryFlow.Json(await f.Analyst.GetAsync($"/api/v1/simulations/{run.GetProperty("simulationRunId")}")); Assert.False(current.GetProperty("isCurrent").GetBoolean());
        using var rejected = await f.Approve(run); Assert.Equal(HttpStatusCode.UnprocessableEntity, rejected.StatusCode);
        var rerun = await f.Simulate(scenario); Assert.NotEqual(run.GetProperty("simulationRunId").GetGuid(), rerun.GetProperty("simulationRunId").GetGuid());
        Assert.False(rerun.GetProperty("evaluations").EnumerateArray().Single(e => e.GetProperty("quotationId").GetString() == quoteId.ToString()).GetProperty("isEligible").GetBoolean());
        Assert.Single((await StoryFlow.Json(await f.Analyst.GetAsync("/api/v1/quotation-expiry-alerts"))).EnumerateArray());
    }
    private static object Variant(JsonElement scenario, decimal price) => new { weights = scenario.GetProperty("criteria").EnumerateArray()
        .Where(c => c.GetProperty("mode").GetString() == "Weighted").Select(c => new { criterionId = c.GetProperty("criterionId"),
            weight = c.GetProperty("category").GetString() == "Price" ? price : 100 - price }).ToArray() };
    private static Task<HttpResponseMessage> Analyze(StoryFlow f, JsonElement scenario, decimal price = 10) => f.Manager.PostAsJsonAsync(
        $"/api/v1/evaluation-scenarios/{scenario.GetProperty("scenarioId")}/sensitivity-analyses", new { configurations = new[] { Variant(scenario, price) } });
    [Fact, Trait("Story", "US19"), Trait("Scenario", "E1"), Trait("Scenario", "E2")]
    public async Task SensitivityPersistsComparableVariantsAndExplainsWinnerChangeWithoutEditingOfficialCriteria()
    {
        using var f = new StoryFlow(); var (scenario, _) = await f.Ready("normal.pdf", "expensive-fast.pdf");
        var result = await StoryFlow.Json(await Analyze(f, scenario), HttpStatusCode.Created); var analysis = result.GetProperty("analysis");
        Assert.False(result.GetProperty("isStable").GetBoolean()); var configs = analysis.GetProperty("configurations");
        Assert.Equal(2, configs.GetArrayLength()); Assert.NotEqual(configs[0].GetProperty("recommendedQuotationId").GetString(), configs[1].GetProperty("recommendedQuotationId").GetString());
        var official = await StoryFlow.Json(await f.Manager.GetAsync($"/api/v1/evaluation-scenarios/{scenario.GetProperty("scenarioId")}"));
        UserStoryAcceptanceTests.AssertEquivalentJson(scenario, official);
        var stored = await StoryFlow.Json(await f.Manager.GetAsync($"/api/v1/sensitivity-analyses/{analysis.GetProperty("id")}")); Assert.True(stored.GetProperty("isCurrent").GetBoolean());
    }
    [Fact, Trait("Story", "US19"), Trait("Scenario", "E1")]
    public async Task SensitivityQueriesSunatOnceAndPreservesIdenticalRateAcrossVariants()
    {
        using var f = new StoryFlow(); var (scenario, _) = await f.Ready("usd.pdf", "expensive-fast.pdf"); var before = f.Factory.ExchangeRate.Calls;
        var result = await StoryFlow.Json(await Analyze(f, scenario), HttpStatusCode.Created);
        Assert.Equal(before + 1, f.Factory.ExchangeRate.Calls); Assert.Equal(3.5m, result.GetProperty("analysis").GetProperty("exchangeRate").GetProperty("rate").GetDecimal());
        await f.Manager.GetAsync($"/api/v1/sensitivity-analyses/{result.GetProperty("analysis").GetProperty("id")}"); Assert.Equal(before + 1, f.Factory.ExchangeRate.Calls);
    }
    [Theory, Trait("Story", "US19"), Trait("Scenario", "E3")]
    [InlineData(true), InlineData(false)]
    public async Task InvalidWeightsOrInsufficientEligibleOffersProduceNoAnalysis(bool invalidWeights)
    {
        using var f = new StoryFlow(); var (scenario, _) = await f.Ready("normal.pdf", invalidWeights ? "expensive.pdf" : "noncompliant.pdf");
        using var result = await Analyze(f, scenario, invalidWeights ? 110 : 60); Assert.Equal(HttpStatusCode.UnprocessableEntity, result.StatusCode);
        using var scope = f.Factory.Services.CreateScope(); Assert.False(await scope.ServiceProvider.GetRequiredService<EvaluationSimulationDbContext>().SensitivityAnalyses.AnyAsync(a => a.RequestId == f.RequestId.ToString()));
    }
    [Fact, Trait("Story", "US19"), Trait("Scenario", "E4")]
    public async Task AnalysisCannotIssueOrdersAndExplicitPromotionCreatesOfficialScenarioAndRunTogether()
    {
        using var f = new StoryFlow(); var (scenario, run) = await f.Ready(); var a = (await StoryFlow.Json(await Analyze(f, scenario), HttpStatusCode.Created)).GetProperty("analysis");
        using var invalid = await f.Manager.PostAsJsonAsync($"/api/v1/simulations/{a.GetProperty("id")}/quotations/{run.GetProperty("recommendation").GetProperty("quotationId")}/purchase-orders", new { deliveryConditions = "Test", deliveryDestination = "Farm" });
        Assert.Equal(HttpStatusCode.NotFound, invalid.StatusCode);
        var promoted = await StoryFlow.Json(await f.Manager.PostAsJsonAsync($"/api/v1/sensitivity-analyses/{a.GetProperty("id")}/configurations/{a.GetProperty("configurations")[1].GetProperty("id")}/official-simulation",
            new { expectedCriteriaVersion = 1 }), HttpStatusCode.Created);
        var official = await StoryFlow.Json(await f.Manager.GetAsync($"/api/v1/simulations/{promoted.GetProperty("simulationRunId")}")); Assert.True(official.GetProperty("isCurrent").GetBoolean());
        using var retry = await f.Manager.PostAsJsonAsync($"/api/v1/sensitivity-analyses/{a.GetProperty("id")}/configurations/{a.GetProperty("configurations")[1].GetProperty("id")}/official-simulation", new { expectedCriteriaVersion = 1 });
        Assert.Equal(HttpStatusCode.Conflict, retry.StatusCode);
    }
    private static object MultiPayload() => new { requiredDate = DateTime.UtcNow.AddDays(7).ToString("yyyy-MM-dd"), priority = "Normal",
        items = new[] { "Alimento crecimiento", "Alimento inicio" }.Select(description => new { description, quantity = 100m, unitOfMeasure = "kg",
            requirements = new[] { new { name = "Proteína mínima", @operator = "GreaterThanOrEqual", expectedValue = "20", unitOfMeasure = "%", isMandatory = true } } }).ToArray() };
    private static async Task<JsonElement> Distributed(StoryFlow f, bool usd = false, bool incomplete = false)
    {
        await f.Create(true, MultiPayload()); await f.Quote(incomplete ? "multi-short.pdf" : usd ? "multi-usd.pdf" : "multi.pdf"); await f.Quote(incomplete ? "multi-other-short.pdf" : "multi-other.pdf");
        var criteria = new List<object>(); var order = 1;
        foreach (var item in f.Request.GetProperty("items").EnumerateArray()) criteria.Add(new { name = "Proteína " + order, targetField = item.GetProperty("requirements")[0].GetProperty("requirementId").GetString(),
            category = "TechnicalCompliance", mode = "Mandatory", @operator = "GreaterThanOrEqual", expectedValue = "20", unitOfMeasure = "%", weight = 0, displayOrder = order++ });
        criteria.Add(new { name = "Precio", targetField = "totalPrice", category = "Price", mode = "Weighted", @operator = "LessThanOrEqual", expectedValue = "999999", unitOfMeasure = "PEN", weight = 100, displayOrder = order });
        var scenario = await StoryFlow.Json(await f.Analyst.PostAsJsonAsync("/api/v1/evaluation-scenarios", new { requestId = f.RequestId.ToString(), criteria }), HttpStatusCode.Created);
        await f.Status("Evaluation"); return await StoryFlow.Json(await f.Analyst.PostAsync($"/api/v1/evaluation-scenarios/{scenario.GetProperty("scenarioId")}/distributed-simulations", null), HttpStatusCode.Created);
    }
    private static Task<HttpResponseMessage> Award(StoryFlow f, JsonElement run, string key, string destination = "Farm", long? expectedBudgetVersion = null)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/simulations/{run.GetProperty("simulationRunId")}/purchase-awards")
        { Content = JsonContent.Create(new { deliveryConditions = "Morning delivery", deliveryDestination = destination, expectedBudgetVersion }) };
        message.Headers.Add("Idempotency-Key", key); return f.Manager.SendAsync(message);
    }
    [Fact, Trait("Story", "US21"), Trait("Scenario", "E2")]
    public async Task IncompleteCoverageRemainsReadableButCannotIssueAnyOrder()
    {
        using var f = new StoryFlow(); var run = await Distributed(f, incomplete: true);
        Assert.False(run.GetProperty("distribution").GetProperty("canApprove").GetBoolean());
        Assert.Single(run.GetProperty("distribution").GetProperty("uncoveredItemIds").EnumerateArray());
        using var rejected = await Award(f, run, Guid.NewGuid().ToString()); Assert.Equal(HttpStatusCode.UnprocessableEntity, rejected.StatusCode); await NoAward(f);
    }
    [Fact, Trait("Story", "US19"), Trait("Scenario", "E4")]
    public async Task FailedPromotionDoesNotSupersedeTheOfficialScenario()
    {
        using var f = new StoryFlow(); var (scenario, _) = await f.Ready();
        var a = (await StoryFlow.Json(await Analyze(f, scenario), HttpStatusCode.Created)).GetProperty("analysis");
        // A new verified quotation changes the source set, even though no official criteria were edited.
        await f.Quote("third.pdf");
        using var rejected = await f.Manager.PostAsJsonAsync($"/api/v1/sensitivity-analyses/{a.GetProperty("id")}/configurations/{a.GetProperty("configurations")[0].GetProperty("id")}/official-simulation", new { expectedCriteriaVersion = 1 });
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        var current = await StoryFlow.Json(await f.Manager.GetAsync($"/api/v1/purchase-requests/{f.RequestId}/evaluation-scenario"));
        Assert.Equal(scenario.GetProperty("scenarioId").GetGuid(), current.GetProperty("scenarioId").GetGuid()); Assert.Equal("Active", current.GetProperty("status").GetString());
    }
    [Fact, Trait("Story", "US17"), Trait("Scenario", "E3")]
    public async Task ClosedRequestsNeverCreatePreventiveAlerts()
    {
        using var f = new StoryFlow(); await f.Create(true); var quote = await f.Quote("normal.pdf");
        await SetAlertSource(f, quote.GetProperty("quotationId").GetGuid(), DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-5)).AddDays(1));
        await f.Status("Cancelled"); await Scan(f);
        Assert.Empty((await StoryFlow.Json(await f.Analyst.GetAsync("/api/v1/quotation-expiry-alerts"))).EnumerateArray());
    }
    [Fact, Trait("Story", "US21"), Trait("Scenario", "E1"), Trait("Scenario", "E3")]
    public async Task DistributedAwardIssuesOnePenOrderPerSupplierWithWholeQuantityAndUsdProvenance()
    {
        using var f = new StoryFlow(); var run = await Distributed(f, true); var distribution = run.GetProperty("distribution");
        Assert.True(distribution.GetProperty("canApprove").GetBoolean()); Assert.Equal(550, distribution.GetProperty("totalPen").GetDecimal());
        var award = await StoryFlow.Json(await Award(f, run, Guid.NewGuid().ToString()), HttpStatusCode.Created);
        Assert.Equal(2, award.GetProperty("orders").GetArrayLength()); Assert.Equal(550, award.GetProperty("award").GetProperty("totalPen").GetDecimal());
        Assert.All(award.GetProperty("orders").EnumerateArray(), o => { Assert.Equal("PEN", o.GetProperty("currency").GetString()); Assert.Equal(JsonValueKind.Null, o.GetProperty("quotationId").ValueKind);
            Assert.Equal(100, o.GetProperty("lines")[0].GetProperty("quantity").GetDecimal()); Assert.NotNull(o.GetProperty("lines")[0].GetProperty("sourceQuotationId").GetString()); });
        var request = await StoryFlow.Json(await f.Production.GetAsync($"/api/v1/purchase-requests/{f.RequestId}")); Assert.Equal("Ordered", request.GetProperty("status").GetString());
        using var singular = await f.Manager.GetAsync($"/api/v1/purchase-requests/{f.RequestId}/purchase-order"); Assert.Equal(HttpStatusCode.Conflict, singular.StatusCode);
        var stored = await StoryFlow.Json(await f.Manager.GetAsync($"/api/v1/purchase-awards/{award.GetProperty("award").GetProperty("id")}"));
        UserStoryAcceptanceTests.AssertEquivalentJson(award, stored);
    }
    [Fact, Trait("Story", "US21"), Trait("Scenario", "E3")]
    public async Task GlobalBudgetBlocksCombinedAwardEvenWhenEachOrderFitsIndividually()
    {
        using var f = new StoryFlow(); var run = await Distributed(f);
        await StoryFlow.Json(await f.Manager.PutAsJsonAsync($"/api/v1/purchase-requests/{f.RequestId}/budget", new { amount = 400, currency = "PEN", reason = "Authorized limit", expectedVersion = 0 }));
        using var rejected = await Award(f, run, Guid.NewGuid().ToString()); Assert.Equal(HttpStatusCode.UnprocessableEntity, rejected.StatusCode); await NoAward(f);
    }
    [Fact, Trait("Story", "US21"), Trait("Scenario", "E4")]
    public async Task ConcurrentRetriesReturnExactSameAwardAndOrdersAndRejectDifferentPayload()
    {
        using var f = new StoryFlow(); var run = await Distributed(f); var key = Guid.NewGuid().ToString();
        var responses = await Task.WhenAll(Award(f, run, key), Award(f, run, key));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created); Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        var first = await StoryFlow.Json(responses[0], HttpStatusCode.Created, HttpStatusCode.OK); var second = await StoryFlow.Json(responses[1], HttpStatusCode.Created, HttpStatusCode.OK);
        UserStoryAcceptanceTests.AssertEquivalentJson(first, second);
        using var conflict = await Award(f, run, key, "Different destination"); Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        var retry = await StoryFlow.Json(await Award(f, run, key)); UserStoryAcceptanceTests.AssertEquivalentJson(first, retry);
    }
    [Theory, Trait("Story", "US21"), Trait("Scenario", "E3")]
    [InlineData(true), InlineData(false)]
    public async Task FailureAfterOrderInsertOrDuringLifecycleRollsBackEveryOrderAwardAndRequest(bool failAfterOrders)
    {
        var interceptor = new AwardFailureInterceptor(failAfterOrders);
        using var f = new StoryFlow(overrides: s => {
            if (failAfterOrders) s.AddDbContext<PurchaseOrderingDbContext>((_, o) => o.AddInterceptors(interceptor));
            else s.AddDbContext<SupplyRequestsDbContext>((_, o) => o.AddInterceptors(interceptor));
        });
        var run = await Distributed(f); interceptor.Armed = true;
        using var failed = await Award(f, run, Guid.NewGuid().ToString()); Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode); Assert.True(interceptor.Triggered);
        await NoAward(f); var request = await StoryFlow.Json(await f.Production.GetAsync($"/api/v1/purchase-requests/{f.RequestId}")); Assert.Equal("Evaluation", request.GetProperty("status").GetString());
    }
    private static async Task NoAward(StoryFlow f)
    {
        using var scope = f.Factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<PurchaseOrderingDbContext>();
        Assert.False(await db.PurchaseAwards.AnyAsync(a => a.RequestId == f.RequestId)); Assert.False(await db.PurchaseOrders.AnyAsync(o => o.SourceDecision.PurchaseRequestId == f.RequestId.ToString()));
    }
    private sealed class AwardFailureInterceptor(bool afterOrders) : SaveChangesInterceptor
    {
        public bool Armed { get; set; } public bool Triggered { get; private set; }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData data, int result, CancellationToken ct = default)
        {
            if (Armed && afterOrders && data.Context is PurchaseOrderingDbContext && data.Context.ChangeTracker.Entries<SmartQuote.API.PurchaseOrdering.Domain.Model.Aggregates.PurchaseOrder>().Count() > 1)
            { Triggered = true; Armed = false; throw new InvalidOperationException("Controlled failure after inserting the order set, before commit."); }
            return base.SavedChangesAsync(data, result, ct);
        }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (Armed && !afterOrders && data.Context is SupplyRequestsDbContext && data.Context.ChangeTracker.Entries<SmartQuote.API.SupplyRequests.Domain.Model.Aggregates.PurchaseRequest>().Any(e => e.Entity.Status.ToString() == "Ordered"))
            { Triggered = true; Armed = false; throw new InvalidOperationException("Controlled failure during request transition."); }
            return base.SavingChangesAsync(data, result, ct);
        }
    }
}
