using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartQuote.API.Shared.Domain.Model.ValueObjects;
using SmartQuote.API.SupplyRequests.Application;
using SmartQuote.API.SupplyRequests.Infrastructure.Persistence.EFC.Configuration;
using SmartQuote.Modules.IdentityAccess.Domain.Model.Aggregates;
using SmartQuote.Modules.IdentityAccess.Domain.Model.Enums;
using SmartQuote.Modules.IdentityAccess.Infrastructure.Persistence.EFC.Configuration;
using Xunit;

namespace SmartQuote.Integration.Tests;

public sealed class WorkflowClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => Now;
}

[Collection("API integration"), Trait("Category", "Integration")]
public sealed class RequestWorkflowAcceptanceTests
{
    private static async Task Seed(StoryFlow f, Guid? extraAnalyst = null, AccountStatus extraStatus = AccountStatus.Active)
    {
        using var scope = f.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityAccessDbContext>();
        var accounts = new List<(Guid Id, SmartQuoteRole Role, AccountStatus Status)>
        {
            (f.ProductionId, SmartQuoteRole.ProductionSpecialist, AccountStatus.Active),
            (f.AnalystId, SmartQuoteRole.PurchaseAnalyst, AccountStatus.Active),
            (f.ManagerId, SmartQuoteRole.PurchaseManager, AccountStatus.Active)
        };
        if (extraAnalyst.HasValue) accounts.Add((extraAnalyst.Value, SmartQuoteRole.PurchaseAnalyst, extraStatus));
        foreach (var a in accounts)
            db.UserAccounts.Add(new UserAccount(new UserId(a.Id), $"{a.Id:N}@workflow.test", "Workflow account", "NotUsedForPasswordLogin",
                [a.Role], DateTimeOffset.UtcNow, a.Status));
        await db.SaveChangesAsync();
    }
    private static async Task<JsonElement> Current(StoryFlow f) => await StoryFlow.Json(await f.Production.GetAsync($"/api/v1/purchase-requests/{f.RequestId}"));
    private static async Task<JsonElement> Ask(StoryFlow f)
    {
        var r = await Current(f); var item = r.GetProperty("items")[0];
        return await StoryFlow.Json(await f.Analyst.PostAsJsonAsync($"/api/v1/purchase-requests/{f.RequestId}/clarifications", new
        {
            itemId = item.GetProperty("itemId").GetGuid(), requirementId = item.GetProperty("requirements")[0].GetProperty("requirementId").GetGuid(),
            specialistId = f.ProductionId, question = "¿El mínimo corresponde a proteína cruda en porcentaje?", expectedVersion = r.GetProperty("version").GetInt64()
        }), HttpStatusCode.Created);
    }
    private static async Task<JsonElement> Answer(StoryFlow f, JsonElement c) => await StoryFlow.Json(await f.Production.PostAsJsonAsync(
        $"/api/v1/technical-clarifications/{c.GetProperty("id").GetGuid()}/response", new { answer = "Sí, proteína cruda; mínimo confirmado 21%.", expectedVersion = c.GetProperty("version").GetInt64() }));
    private static object RevisedSpecification() => new { name = "Proteína mínima", @operator = "GreaterThanOrEqual", expectedValue = "21", unitOfMeasure = "%", isMandatory = true };

    // US15/E1: the consultation itself is the durable recipient notification, not a transient event.
    [Fact, Trait("Story", "US15"), Trait("Scenario", "E1")]
    public async Task RegistersLinkedConsultationAndRecipientInbox()
    {
        using var f = new StoryFlow(); await Seed(f); await f.Create();
        var c = await Ask(f);
        Assert.Equal(f.AnalystId, c.GetProperty("authorId").GetGuid()); Assert.Equal(f.RequestId, c.GetProperty("requestId").GetGuid());
        var inbox = await StoryFlow.Json(await f.Production.GetAsync("/api/v1/technical-clarifications?assignedToMe=true&status=Pending"));
        Assert.Contains(inbox.EnumerateArray(), n => n.GetProperty("id").GetGuid() == c.GetProperty("id").GetGuid());
        Assert.True(c.GetProperty("createdAt").GetDateTimeOffset() > DateTimeOffset.UtcNow.AddMinutes(-5));
        var before = await Current(f); var item = before.GetProperty("items")[0];
        using var invalid = await f.Analyst.PostAsJsonAsync($"/api/v1/purchase-requests/{f.RequestId}/clarifications", new
        { itemId = item.GetProperty("itemId"), requirementId = Guid.NewGuid(), specialistId = f.ProductionId, question = "Test", expectedVersion = before.GetProperty("version") });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalid.StatusCode);
    }

    [Fact, Trait("Story", "US15"), Trait("Scenario", "E2")]
    public async Task OnlyRecipientAnswersWithoutChangingSpecification()
    {
        using var f = new StoryFlow(); await Seed(f); await f.Create(); var c = await Ask(f);
        using var stranger = f.Client("ProductionSpecialist");
        using var denied = await stranger.PostAsJsonAsync($"/api/v1/technical-clarifications/{c.GetProperty("id")}/response", new { answer = "No", expectedVersion = 1 });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        var answered = await Answer(f, c); Assert.Equal("Answered", answered.GetProperty("status").GetString());
        Assert.Equal(f.ProductionId, answered.GetProperty("answeredBy").GetGuid());
        var r = await Current(f); Assert.Equal("20", r.GetProperty("items")[0].GetProperty("requirements")[0].GetProperty("expectedValue").GetString());
        var list = await StoryFlow.Json(await f.Analyst.GetAsync($"/api/v1/purchase-requests/{f.RequestId}/clarifications"));
        Assert.Equal(answered.GetProperty("answer").GetString(), list[0].GetProperty("answer").GetString());
        using var read = await f.Production.PutAsync($"/api/v1/technical-clarifications/{c.GetProperty("id")}/read", null);
        Assert.Equal(HttpStatusCode.NoContent, read.StatusCode);
    }

    [Fact, Trait("Story", "US15"), Trait("Scenario", "E3")]
    public async Task RevisionRejectsOldDecisionAndRequiresCurrentMandatoryCriteria()
    {
        using var f = new StoryFlow(); await Seed(f); var (scenario, run) = await f.Ready();
        var answered = await Answer(f, await Ask(f)); var r = await Current(f);
        var revision = await StoryFlow.Json(await f.Production.PostAsJsonAsync($"/api/v1/technical-clarifications/{answered.GetProperty("id")}/requirement-revisions",
            new { specification = RevisedSpecification(), reason = "Confirmación del especialista", expectedClarificationVersion = answered.GetProperty("version"), expectedRequestVersion = r.GetProperty("version") }), HttpStatusCode.Created);
        Assert.Equal("20", revision.GetProperty("previous").GetProperty("expectedValue").GetString());
        Assert.Equal("21", revision.GetProperty("current").GetProperty("expectedValue").GetString());
        var old = await StoryFlow.Json(await f.Analyst.GetAsync($"/api/v1/simulations/{run.GetProperty("simulationRunId")}"));
        Assert.False(old.GetProperty("isCurrent").GetBoolean());
        using var approval = await f.Approve(run); Assert.Equal(HttpStatusCode.UnprocessableEntity, approval.StatusCode);
        using var rerun = await f.Analyst.PostAsync($"/api/v1/evaluation-scenarios/{scenario.GetProperty("scenarioId")}/simulations", null);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, rerun.StatusCode);
        using var staleCriteria = await f.Analyst.PostAsJsonAsync($"/api/v1/evaluation-scenarios/{scenario.GetProperty("scenarioId")}/versions", new { criteria = f.Criteria() });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, staleCriteria.StatusCode);
        var criteria = JsonNode.Parse(JsonSerializer.Serialize(f.Criteria()))!; criteria[0]!["expectedValue"] = "21";
        var updated = await StoryFlow.Json(await f.Analyst.PostAsJsonAsync($"/api/v1/evaluation-scenarios/{scenario.GetProperty("scenarioId")}/versions", new { criteria }), HttpStatusCode.Created);
        var fresh = await f.Simulate(updated); Assert.True(fresh.GetProperty("isCurrent").GetBoolean());
        using var accepted = await f.Approve(fresh); Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
        var history = await StoryFlow.Json(await f.Production.GetAsync($"/api/v1/purchase-requests/{f.RequestId}/requirement-revisions")); Assert.Single(history.EnumerateArray());
    }

    private static WorkflowClock Clock() => new(DateTimeOffset.UtcNow);
    private static async Task<JsonElement> Schedule(StoryFlow f, WorkflowClock clock)
    {
        var template = JsonSerializer.SerializeToElement(StoryFlow.Payload());
        return await StoryFlow.Json(await f.Production.PostAsJsonAsync("/api/v1/recurring-request-schedules", new
        { startDate = DateOnly.FromDateTime(clock.Now.UtcDateTime.AddHours(-5)), intervalDays = 7, requiredDateOffsetDays = 2,
            timeZoneId = "America/Lima", priority = "Normal", items = template.GetProperty("items") }), HttpStatusCode.Created);
    }
    private static async Task<int> Process(StoryFlow f, Guid id)
    {
        using var scope = f.Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<RecurringOccurrenceProcessor>().ProcessAsync(id, 10);
    }
    private static async Task<JsonElement> ReadSchedule(StoryFlow f, Guid id) => await StoryFlow.Json(await f.Production.GetAsync($"/api/v1/recurring-request-schedules/{id}"));

    [Fact, Trait("Story", "US16"), Trait("Scenario", "E1")]
    public async Task ValidScheduleStoresOwnerZoneAndDoesNotIssueOrder()
    {
        var clock = Clock(); using var f = new StoryFlow(clock); await Seed(f);
        var schedule = await Schedule(f, clock); Assert.Equal(f.ProductionId, schedule.GetProperty("ownerId").GetGuid());
        Assert.Equal("America/Lima", schedule.GetProperty("timeZoneId").GetString()); Assert.Empty(schedule.GetProperty("occurrences").EnumerateArray());
        using var bad = await f.Production.PostAsJsonAsync("/api/v1/recurring-request-schedules", new
        { startDate = DateOnly.FromDateTime(clock.Now.DateTime), intervalDays = 0, requiredDateOffsetDays = 2, timeZoneId = "America/Lima", priority = "Normal", items = JsonSerializer.SerializeToElement(StoryFlow.Payload()).GetProperty("items") });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, bad.StatusCode);
        using var scope = f.Factory.Services.CreateScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<SupplyRequestsDbContext>().PurchaseRequests.AnyAsync(r => r.RequesterId == new UserId(f.ProductionId)));
    }

    [Fact, Trait("Story", "US16"), Trait("Scenario", "E2")]
    public async Task GeneratedDraftIsPrivateEditableAndRequiresExplicitConfirmation()
    {
        var clock = Clock(); using var f = new StoryFlow(clock); await Seed(f); var s = await Schedule(f, clock); var id = s.GetProperty("id").GetGuid();
        Assert.Equal(1, await Process(f, id)); s = await ReadSchedule(f, id);
        var requestId = s.GetProperty("occurrences")[0].GetProperty("requestId").GetGuid();
        var draft = await StoryFlow.Json(await f.Production.GetAsync($"/api/v1/purchase-requests/{requestId}")); Assert.Equal("Draft", draft.GetProperty("status").GetString());
        using var denied = await f.Analyst.GetAsync($"/api/v1/purchase-requests/{requestId}"); Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        var purchasing = await StoryFlow.Json(await f.Analyst.GetAsync("/api/v1/purchase-requests?status=Draft")); Assert.Equal(0, purchasing.GetProperty("totalItems").GetInt32());
        var edited = await StoryFlow.Json(await f.Production.PutAsJsonAsync($"/api/v1/purchase-requests/{requestId}/draft", new
        { requiredDate = DateOnly.FromDateTime(clock.Now.UtcDateTime.AddDays(5)), priority = "High", items = JsonSerializer.SerializeToElement(StoryFlow.Payload(quantity: 1200)).GetProperty("items"), expectedVersion = draft.GetProperty("version") }));
        Assert.Equal(1200, edited.GetProperty("items")[0].GetProperty("quantity").GetDecimal());
        var submitted = await StoryFlow.Json(await f.Production.PostAsJsonAsync($"/api/v1/purchase-requests/{requestId}/submit", new { expectedVersion = edited.GetProperty("version") }));
        Assert.Equal("Submitted", submitted.GetProperty("status").GetString());
        using var visible = await f.Analyst.GetAsync($"/api/v1/purchase-requests/{requestId}"); Assert.Equal(HttpStatusCode.OK, visible.StatusCode);
    }

    [Fact, Trait("Story", "US16"), Trait("Scenario", "E3")]
    public async Task ConcurrentOccurrenceRetryProducesOneDraftAndPreservesHumanEdits()
    {
        var clock = Clock(); using var f = new StoryFlow(clock); await Seed(f); var s = await Schedule(f, clock); var id = s.GetProperty("id").GetGuid();
        var counts = await Task.WhenAll(Process(f, id), Process(f, id)); Assert.Equal(1, counts.Sum());
        s = await ReadSchedule(f, id); Assert.Single(s.GetProperty("occurrences").EnumerateArray());
        var requestId = s.GetProperty("occurrences")[0].GetProperty("requestId").GetGuid();
        var draft = await StoryFlow.Json(await f.Production.GetAsync($"/api/v1/purchase-requests/{requestId}"));
        await StoryFlow.Json(await f.Production.PutAsJsonAsync($"/api/v1/purchase-requests/{requestId}/draft", new
        { requiredDate = DateOnly.FromDateTime(clock.Now.UtcDateTime.AddDays(5)), priority = "High", items = JsonSerializer.SerializeToElement(StoryFlow.Payload(quantity: 1250)).GetProperty("items"), expectedVersion = draft.GetProperty("version") }));
        Assert.Equal(0, await Process(f, id));
        draft = await StoryFlow.Json(await f.Production.GetAsync($"/api/v1/purchase-requests/{requestId}")); Assert.Equal(1250, draft.GetProperty("items")[0].GetProperty("quantity").GetDecimal());
    }

    [Fact, Trait("Story", "US16"), Trait("Scenario", "E4")]
    public async Task PausePreventsFutureGenerationAndKeepsHistory()
    {
        var clock = Clock(); using var f = new StoryFlow(clock); await Seed(f); var s = await Schedule(f, clock); var id = s.GetProperty("id").GetGuid();
        await Process(f, id); s = await ReadSchedule(f, id);
        s = await StoryFlow.Json(await f.Production.PostAsJsonAsync($"/api/v1/recurring-request-schedules/{id}/pause", new { expectedVersion = s.GetProperty("version") }));
        Assert.False(s.GetProperty("isActive").GetBoolean()); clock.Now = clock.Now.AddDays(20);
        Assert.Equal(0, await Process(f, id)); s = await ReadSchedule(f, id); Assert.Single(s.GetProperty("occurrences").EnumerateArray());
    }

    [Fact, Trait("Story", "US22"), Trait("Scenario", "E1")]
    public async Task ManagerAssignsActiveAnalystWithoutChangingRequesterOrState()
    {
        using var f = new StoryFlow(); await Seed(f); await f.Create();
        var response = await StoryFlow.Json(await f.Manager.PutAsJsonAsync($"/api/v1/purchase-requests/{f.RequestId}/assignment", new
        { analystId = f.AnalystId, expectedVersion = f.Request.GetProperty("version") }));
        Assert.Equal(f.AnalystId, response.GetProperty("assignedAnalystId").GetGuid()); Assert.Equal(f.ProductionId, response.GetProperty("requesterId").GetGuid()); Assert.Equal("Submitted", response.GetProperty("status").GetString());
        var directory = await StoryFlow.Json(await f.Manager.GetAsync("/api/v1/iam/users?role=PurchaseAnalyst&status=Active&pageSize=100"));
        Assert.Contains(directory.EnumerateArray(), u => u.GetProperty("userId").GetGuid() == f.AnalystId);
        var history = await StoryFlow.Json(await f.Manager.GetAsync($"/api/v1/purchase-requests/{f.RequestId}/assignment-history")); Assert.Equal(f.ManagerId, history[0].GetProperty("assignedBy").GetGuid());
    }

    [Fact, Trait("Story", "US22"), Trait("Scenario", "E2")]
    public async Task ConcurrentReassignmentsKeepSingleOwnerAndCompleteHistory()
    {
        using var f = new StoryFlow(); var b = Guid.NewGuid(); await Seed(f, b); await f.Create();
        var first = await StoryFlow.Json(await f.Manager.PutAsJsonAsync($"/api/v1/purchase-requests/{f.RequestId}/assignment", new { analystId = f.AnalystId, expectedVersion = 1 }));
        var body = new { analystId = b, reason = "Balance de carga", expectedVersion = first.GetProperty("version") };
        var responses = await Task.WhenAll(f.Manager.PutAsJsonAsync($"/api/v1/purchase-requests/{f.RequestId}/assignment", body), f.Manager.PutAsJsonAsync($"/api/v1/purchase-requests/{f.RequestId}/assignment", body));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict); foreach (var response in responses) response.Dispose();
        var history = await StoryFlow.Json(await f.Manager.GetAsync($"/api/v1/purchase-requests/{f.RequestId}/assignment-history")); Assert.Equal(2, history.GetArrayLength());
        Assert.Equal(b, (await Current(f)).GetProperty("assignedAnalystId").GetGuid());
    }

    [Fact, Trait("Story", "US22"), Trait("Scenario", "E3")]
    public async Task InactiveAccountWrongRoleAndInactiveRequestCannotBeAssigned()
    {
        using var f = new StoryFlow(); var disabled = Guid.NewGuid(); await Seed(f, disabled, AccountStatus.Disabled); await f.Create();
        foreach (var target in new[] { disabled, f.ProductionId })
        {
            using var denied = await f.Manager.PutAsJsonAsync($"/api/v1/purchase-requests/{f.RequestId}/assignment", new { analystId = target, expectedVersion = 1 });
            Assert.Equal(HttpStatusCode.UnprocessableEntity, denied.StatusCode);
        }
        await f.Status("Cancelled"); var before = await Current(f);
        using var cancelled = await f.Manager.PutAsJsonAsync($"/api/v1/purchase-requests/{f.RequestId}/assignment", new { analystId = f.AnalystId, expectedVersion = before.GetProperty("version") });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, cancelled.StatusCode); Assert.Equal(JsonValueKind.Null, (await Current(f)).GetProperty("assignedAnalystId").ValueKind);
    }

    [Fact, Trait("Story", "US22"), Trait("Scenario", "E4")]
    public async Task MyQueueFollowsCurrentAssignmentWithoutGrantingManagerPowers()
    {
        using var f = new StoryFlow(); var b = Guid.NewGuid(); await Seed(f, b); await f.Create();
        var first = await StoryFlow.Json(await f.Manager.PutAsJsonAsync($"/api/v1/purchase-requests/{f.RequestId}/assignment", new { analystId = f.AnalystId, expectedVersion = 1 }));
        var mine = await StoryFlow.Json(await f.Analyst.GetAsync("/api/v1/purchase-requests?assignedToMe=true")); Assert.Equal(1, mine.GetProperty("totalItems").GetInt32());
        Assert.True(mine.GetProperty("items")[0].TryGetProperty("requiredDate", out _)); Assert.True(mine.GetProperty("items")[0].TryGetProperty("priority", out _));
        await StoryFlow.Json(await f.Manager.PutAsJsonAsync($"/api/v1/purchase-requests/{f.RequestId}/assignment", new { analystId = b, reason = "Reasignación", expectedVersion = first.GetProperty("version") }));
        mine = await StoryFlow.Json(await f.Analyst.GetAsync("/api/v1/purchase-requests?assignedToMe=true")); Assert.Equal(0, mine.GetProperty("totalItems").GetInt32());
        using var other = f.Client("PurchaseAnalyst", b); var theirs = await StoryFlow.Json(await other.GetAsync("/api/v1/purchase-requests?assignedToMe=true")); Assert.Equal(1, theirs.GetProperty("totalItems").GetInt32());
        using var denied = await f.Analyst.PutAsJsonAsync($"/api/v1/purchase-requests/{f.RequestId}/assignment", new { analystId = f.AnalystId, expectedVersion = 3 }); Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    // US16/E2: the real hosted worker, not an HTTP test-only endpoint, generates a draft.
    [Fact, Trait("Story", "US16"), Trait("Scenario", "E2")]
    public async Task HostedWorkerAutomaticallyProcessesDueSchedule()
    {
        var clock = Clock(); using var f = new StoryFlow(clock, enableRecurringWorker: true); await Seed(f);
        var s = await Schedule(f, clock); var id = s.GetProperty("id").GetGuid();
        var deadline = DateTime.UtcNow.AddSeconds(12);
        do
        {
            await Task.Delay(150); s = await ReadSchedule(f, id);
        } while (s.GetProperty("occurrences").GetArrayLength() == 0 && DateTime.UtcNow < deadline);
        Assert.Single(s.GetProperty("occurrences").EnumerateArray());
    }

    [Fact, Trait("Story", "US16"), Trait("Scenario", "E4")]
    public async Task PauseRaceWithGenerationCannotCreateAfterPauseCommits()
    {
        var clock = Clock(); using var f = new StoryFlow(clock); await Seed(f); var s = await Schedule(f, clock); var id = s.GetProperty("id").GetGuid();
        var generation = Process(f, id);
        using var pause = await f.Production.PostAsJsonAsync($"/api/v1/recurring-request-schedules/{id}/pause", new { expectedVersion = 1 });
        await generation;
        Assert.Contains(pause.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict });
        s = await ReadSchedule(f, id);
        if (s.GetProperty("isActive").GetBoolean())
            s = await StoryFlow.Json(await f.Production.PostAsJsonAsync($"/api/v1/recurring-request-schedules/{id}/pause", new { expectedVersion = s.GetProperty("version") }));
        var previousCount = s.GetProperty("occurrences").GetArrayLength(); clock.Now = clock.Now.AddDays(8);
        Assert.Equal(0, await Process(f, id));
        Assert.Equal(previousCount, (await ReadSchedule(f, id)).GetProperty("occurrences").GetArrayLength());
    }

    // US22 changes responsibility, not the frozen technical/commercial inputs of a decision.
    [Fact, Trait("Story", "US22"), Trait("Scenario", "E1")]
    public async Task AdministrativeAssignmentDoesNotInvalidateSimulation()
    {
        using var f = new StoryFlow(); await Seed(f); var (_, run) = await f.Ready(); var r = await Current(f);
        await StoryFlow.Json(await f.Manager.PutAsJsonAsync($"/api/v1/purchase-requests/{f.RequestId}/assignment", new { analystId = f.AnalystId, expectedVersion = r.GetProperty("version") }));
        var result = await StoryFlow.Json(await f.Analyst.GetAsync($"/api/v1/simulations/{run.GetProperty("simulationRunId")}"));
        Assert.True(result.GetProperty("isCurrent").GetBoolean());
        using var approval = await f.Approve(run); Assert.Equal(HttpStatusCode.Created, approval.StatusCode);
    }
}
