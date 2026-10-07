using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using SmartQuote.API.SupplyRequests.Infrastructure.Persistence.EFC.Configuration;
using SmartQuote.API.PurchaseOrdering.Infrastructure.Persistence.EFC.Configuration;
using SmartQuote.Modules.EvaluationSimulation.Infrastructure.Persistence.EFC.Configuration;
using SmartQuote.Modules.IdentityAccess.Infrastructure.Persistence.EFC.Configuration;
using Xunit;

namespace SmartQuote.Integration.Tests;

[Collection("API integration")]
[Trait("Category", "Integration")]
public sealed class UserStoryAcceptanceTests
{
    // TS01/E3: fallo del proveedor externo conserva el documento y comunica 503 seguro.
    [Fact, Trait("Story", "TS01"), Trait("Scenario", "E3")]
    public async Task ExternalExtractionFailureIsStructuredAndDocumentRemainsRegistered()
    {
        using var f = new StoryFlow(); await f.Create(true);
        using var form = StoryFlow.File("external-unavailable.pdf");
        var uploaded = await StoryFlow.Json(await f.Analyst.PostAsync($"/api/v1/purchase-requests/{f.RequestId}/quotations", form), HttpStatusCode.Created);
        var id = uploaded.GetProperty("quotationId").GetGuid();
        var problem = await StoryFlow.Json(await f.Analyst.PostAsync($"/api/v1/quotations/{id}/process", null), HttpStatusCode.ServiceUnavailable);
        Assert.True(problem.TryGetProperty("traceId", out _)); Assert.True(problem.TryGetProperty("code", out _));
        Assert.DoesNotContain("HttpRequestException", problem.GetRawText());
        var stored = await StoryFlow.Json(await f.Analyst.GetAsync($"/api/v1/quotations/{id}"));
        Assert.Equal(id, stored.GetProperty("quotationId").GetGuid()); Assert.Equal("Rejected", stored.GetProperty("status").GetString());
    }

    // US08/E2: cambia un dato de decisión; no una mera transición de estado.
    // Preparación directa de la base de pruebas: no se inventa un endpoint de edición.
    [Fact, Trait("Story", "US08"), Trait("Scenario", "E2")]
    public async Task ChangedRequestSnapshotCannotAuthorizeAnOldSimulation()
    {
        using var f = new StoryFlow(); var (_, run) = await f.Ready();
        using (var changed = f.Factory.Services.CreateScope())
        {
            var requests = changed.ServiceProvider.GetRequiredService<SupplyRequestsDbContext>();
            var entity = (await requests.PurchaseRequests.ToListAsync()).Single(r => r.Id.Value == f.RequestId);
            requests.Entry(entity).Property(r => r.RequiredDate).CurrentValue = entity.RequiredDate.AddDays(1);
            requests.Entry(entity).Property(r => r.Version).CurrentValue = entity.Version + 1;
            await requests.SaveChangesAsync();
        }
        var current = await StoryFlow.Json(await f.Analyst.GetAsync($"/api/v1/simulations/{run.GetProperty("simulationRunId")}"));
        Assert.False(current.GetProperty("isCurrent").GetBoolean());
        using var approval = await f.Approve(run); Assert.Equal(HttpStatusCode.UnprocessableEntity, approval.StatusCode);
        using var scope = f.Factory.Services.CreateScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<PurchaseOrderingDbContext>().PurchaseOrders.AnyAsync(o => o.SourceDecision.PurchaseRequestId == f.RequestId.ToString()));
    }

    // US10/E1-E3: resultados por archivo consultables; un PDF ilegible no bloquea otro.
    [Fact, Trait("Story", "US10"), Trait("Scenario", "E1"), Trait("Scenario", "E2"), Trait("Scenario", "E3")]
    public async Task BatchResultsAreIndependentAndCanBeReloadedAfterFailure()
    {
        using var f = new StoryFlow(); await f.Create(true);
        using var form = new MultipartFormDataContent();
        foreach (var name in new[] { "normal.pdf", "unreadable.pdf", "expensive.pdf" })
        {
            var file = new ByteArrayContent(Encoding.ASCII.GetBytes("%PDF-1.4\n" + name + Guid.NewGuid() + "\n%%EOF\n"));
            file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            form.Add(file, "files", name);
        }
        var uploaded = await StoryFlow.Json(await f.Analyst.PostAsync($"/api/v1/purchase-requests/{f.RequestId}/quotations/batch", form), HttpStatusCode.MultiStatus);
        Assert.Equal(3, uploaded.GetArrayLength());
        foreach (var result in uploaded.EnumerateArray()) Assert.True(result.GetProperty("wasCreated").GetBoolean());
        var broken = Assert.Single(uploaded.EnumerateArray(), item => item.GetProperty("fileName").GetString() == "unreadable.pdf");
        using var failure = await f.Analyst.PostAsync($"/api/v1/quotations/{broken.GetProperty("quotationId")}/process", null);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, failure.StatusCode);
        foreach (var item in uploaded.EnumerateArray().Where(item => item.GetProperty("fileName").GetString() != "unreadable.pdf"))
            await f.Process(item.GetProperty("quotationId").GetGuid());
        var reloaded = await StoryFlow.Json(await f.Analyst.GetAsync($"/api/v1/purchase-requests/{f.RequestId}/quotations"));
        Assert.Equal(3, reloaded.GetArrayLength());
        Assert.Equal(2, reloaded.EnumerateArray().Count(q => q.GetProperty("status").GetString() == "Verified"));
        Assert.Equal("Rejected", Assert.Single(reloaded.EnumerateArray(), q => q.GetProperty("quotationId").GetGuid() == broken.GetProperty("quotationId").GetGuid()).GetProperty("status").GetString());
    }

    // PostgreSQL conserva microsegundos; comparar números y fechas, no su formato JSON.
    private static void AssertEquivalentJson(JsonElement expected, JsonElement actual)
    {
        Assert.Equal(expected.ValueKind, actual.ValueKind);
        switch (expected.ValueKind)
        {
            case JsonValueKind.Object:
                Assert.Equal(expected.EnumerateObject().Count(), actual.EnumerateObject().Count());
                foreach (var property in expected.EnumerateObject()) AssertEquivalentJson(property.Value, actual.GetProperty(property.Name));
                break;
            case JsonValueKind.Array:
                Assert.Equal(expected.GetArrayLength(), actual.GetArrayLength());
                var expectedItems = expected.EnumerateArray().ToArray();
                var actualItems = actual.EnumerateArray().ToArray();
                var identity = new[] { "fieldId", "lineId", "quotationId", "criterionId", "name" }
                    .FirstOrDefault(key => expectedItems.Length > 0 && expectedItems.All(e => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(key, out _)) && actualItems.All(e => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(key, out _)));
                if (identity is not null) { expectedItems = expectedItems.OrderBy(e => e.GetProperty(identity).ToString()).ToArray(); actualItems = actualItems.OrderBy(e => e.GetProperty(identity).ToString()).ToArray(); }
                for (var i = 0; i < expectedItems.Length; i++) AssertEquivalentJson(expectedItems[i], actualItems[i]);
                break;
            case JsonValueKind.Number: Assert.Equal(expected.GetDecimal(), actual.GetDecimal()); break;
            case JsonValueKind.String:
                if (expected.TryGetDateTimeOffset(out var time) && actual.TryGetDateTimeOffset(out var other))
                    Assert.True((time - other).Duration() <= TimeSpan.FromMicroseconds(1));
                else Assert.Equal(expected.GetString(), actual.GetString());
                break;
            default: Assert.Equal(expected.GetRawText(), actual.GetRawText()); break;
        }
    }
    // US02/E1: se relee una solicitud completa con su solicitante y datos de negocio.
    [Fact, Trait("Story", "US02"), Trait("Scenario", "E1")]
    public async Task CompleteRequestRetainsBusinessDataAndRequester()
    {
        using var f = new StoryFlow(); await f.Create();
        var stored = await StoryFlow.Json(await f.Production.GetAsync($"/api/v1/purchase-requests/{f.RequestId}"));
        Assert.Equal(f.ProductionId, stored.GetProperty("requesterId").GetGuid());
        Assert.Equal("Submitted", stored.GetProperty("status").GetString());
        Assert.Equal("High", stored.GetProperty("priority").GetString());
        Assert.Equal(DateTime.UtcNow.AddDays(7).ToString("yyyy-MM-dd"), stored.GetProperty("requiredDate").GetString());
        Assert.Equal(1000m, stored.GetProperty("items")[0].GetProperty("quantity").GetDecimal());
        Assert.True(stored.GetProperty("items")[0].GetProperty("requirements")[0].GetProperty("isMandatory").GetBoolean());
        using var scope = f.Factory.Services.CreateScope();
        Assert.Contains(await scope.ServiceProvider.GetRequiredService<SupplyRequestsDbContext>().PurchaseRequests.ToListAsync(), r => r.Id.Value == f.RequestId);
    }

    // US02/E2: cantidades inválidas o ausencia de requisitos obligatorios no crean filas.
    [Theory, InlineData(false, 1000), InlineData(true, 0), InlineData(true, -1)]
    [Trait("Story", "US02"), Trait("Scenario", "E2")]
    public async Task InvalidRequestDoesNotPersist(bool mandatory, int quantity)
    {
        using var f = new StoryFlow(); using var scope = f.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SupplyRequestsDbContext>();
        var count = await db.PurchaseRequests.CountAsync();
        using var response = await f.Production.PostAsJsonAsync("/api/v1/purchase-requests", StoryFlow.Payload(mandatory, quantity));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(count, await db.PurchaseRequests.CountAsync());
    }

    // US02/E3: el sustento mantiene nombre, tipo, autor y fecha tras consultar de nuevo.
    [Fact, Trait("Story", "US02"), Trait("Scenario", "E3")]
    public async Task AttachmentPreservesAllRequiredMetadata()
    {
        using var f = new StoryFlow(); await f.Create(); var start = DateTimeOffset.UtcNow;
        using var form = StoryFlow.File("sustento-tecnico.pdf");
        form.Add(new StringContent(f.Request.GetProperty("version").GetInt64().ToString()), "expectedVersion");
        using var response = await f.Production.PostAsync($"/api/v1/purchase-requests/{f.RequestId}/attachments", form);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var stored = await StoryFlow.Json(await f.Production.GetAsync($"/api/v1/purchase-requests/{f.RequestId}"));
        var attachment = Assert.Single(stored.GetProperty("attachments").EnumerateArray());
        Assert.Equal("sustento-tecnico.pdf", attachment.GetProperty("fileName").GetString());
        Assert.Equal("application/pdf", attachment.GetProperty("contentType").GetString());
        Assert.Equal(f.ProductionId, attachment.GetProperty("uploadedBy").GetGuid());
        Assert.InRange(attachment.GetProperty("uploadedAt").GetDateTimeOffset(), start, DateTimeOffset.UtcNow);
    }

    // US03/E1-E3: estado, área, notificación al solicitante e historial real ordenado.
    [Fact, Trait("Story", "US03"), Trait("Scenario", "E1"), Trait("Scenario", "E2"), Trait("Scenario", "E3")]
    public async Task StatusHistoryAndNotificationsRetainActorsAndChronology()
    {
        using var f = new StoryFlow(); await f.Create(true);
        Assert.Equal("QuotationCollection", f.Request.GetProperty("status").GetString());
        Assert.False(string.IsNullOrWhiteSpace(f.Request.GetProperty("nextResponsibleArea").GetString()));
        Assert.True(f.Request.GetProperty("updatedAt").GetDateTimeOffset() >= f.Request.GetProperty("createdAt").GetDateTimeOffset());
        var history = await StoryFlow.Json(await f.Production.GetAsync($"/api/v1/purchase-requests/{f.RequestId}/history"));
        var entries = history.GetProperty("entries").EnumerateArray().ToArray();
        Assert.Equal(new[] { "Submitted", "UnderReview", "QuotationCollection" }, entries.Select(e => e.GetProperty("toStatus").GetString()));
        Assert.Equal(f.AnalystId, entries.Last().GetProperty("changedBy").GetGuid());
        Assert.Equal("Story acceptance verification", entries.Last().GetProperty("reason").GetString());
        Assert.Equal(entries.Select(e => e.GetProperty("changedAt").GetDateTimeOffset()).Order(), entries.Select(e => e.GetProperty("changedAt").GetDateTimeOffset()));
        var notifications = await StoryFlow.Json(await f.Production.GetAsync("/api/v1/notifications"));
        Assert.Contains(notifications.EnumerateArray(), n => n.GetProperty("purchaseRequestId").GetGuid() == f.RequestId && n.GetProperty("newStatus").GetString() == "QuotationCollection");
        using var other = f.Client("ProductionSpecialist");
        Assert.DoesNotContain((await StoryFlow.Json(await other.GetAsync("/api/v1/notifications"))).EnumerateArray(), n => n.GetProperty("purchaseRequestId").GetGuid() == f.RequestId);
    }

    // TS01/E1 y US04/E1: extracción controlada con evidencia, servicio y persistencia reales.
    [Fact, Trait("Story", "TS01"), Trait("Story", "US04"), Trait("Scenario", "E1")]
    public async Task ExtractionReturnsStructuredFieldsAndSourceEvidence()
    {
        using var f = new StoryFlow(); await f.Create(true); var quote = await f.Quote("normal.pdf", false);
        Assert.Equal(f.RequestId.ToString(), quote.GetProperty("requestId").GetString());
        Assert.Equal("Supplier normal.pdf", quote.GetProperty("supplierBusinessName").GetString());
        Assert.Equal("20123456789", quote.GetProperty("supplierTaxIdentifier").GetString());
        Assert.Equal("PEN", quote.GetProperty("currency").GetString());
        Assert.Equal(3, quote.GetProperty("deliveryLeadTimeDays").GetInt32());
        Assert.Equal(1000m, quote.GetProperty("lines")[0].GetProperty("quantity").GetDecimal());
        Assert.NotEmpty(quote.GetProperty("lines")[0].GetProperty("specifications").EnumerateArray());
        Assert.All(quote.GetProperty("fields").EnumerateArray(), field =>
        {
            Assert.Equal(1m, field.GetProperty("confidence").GetDecimal());
            Assert.Equal(1, field.GetProperty("sourcePageNumber").GetInt32());
            Assert.False(string.IsNullOrWhiteSpace(field.GetProperty("sourceTextReference").GetString()));
        });
        var reloaded = await StoryFlow.Json(await f.Analyst.GetAsync($"/api/v1/quotations/{quote.GetProperty("quotationId")}"));
        AssertEquivalentJson(quote, reloaded);
    }

    // TS01/E2 y US05/E3: la baja confianza o ausencia se conserva y bloquea confirmar.
    [Theory, InlineData("missing.pdf"), InlineData("low-confidence.pdf")]
    [Trait("Story", "TS01"), Trait("Scenario", "E2"), Trait("Story", "US05"), Trait("Scenario", "E3")]
    public async Task UnresolvedExtractionCannotBeConfirmed(string name)
    {
        using var f = new StoryFlow(); await f.Create(true); var quote = await f.Quote(name, false);
        var field = quote.GetProperty("fields").EnumerateArray().Single(v => v.GetProperty("fieldPath").GetString() == "lines[0].unitPrice");
        Assert.Equal("Unresolved", field.GetProperty("status").GetString());
        if (name == "missing.pdf") Assert.Equal(JsonValueKind.Null, field.GetProperty("originalValue").ValueKind);
        else Assert.Equal(0.3m, field.GetProperty("confidence").GetDecimal());
        using var rejected = await f.Analyst.PostAsJsonAsync($"/api/v1/quotations/{quote.GetProperty("quotationId")}/confirm", new { expectedVersion = quote.GetProperty("version").GetInt64(), lineMappings = Array.Empty<object>() });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, rejected.StatusCode);
        using var noScenario = await f.Analyst.GetAsync($"/api/v1/purchase-requests/{f.RequestId}/evaluation-scenario");
        Assert.Equal(HttpStatusCode.NotFound, noScenario.StatusCode);
    }

    // TS01/E3 y US04/E2: el fallo de lectura no filtra datos internos ni permite verificar.
    [Fact, Trait("Story", "TS01"), Trait("Scenario", "E3"), Trait("Story", "US04"), Trait("Scenario", "E2")]
    public async Task UnreadableDocumentHasStructuredErrorAndRejectedState()
    {
        using var f = new StoryFlow(); await f.Create(true); using var form = StoryFlow.File("unreadable.pdf");
        var quote = await StoryFlow.Json(await f.Analyst.PostAsync($"/api/v1/purchase-requests/{f.RequestId}/quotations", form), HttpStatusCode.Created);
        var error = await StoryFlow.Json(await f.Analyst.PostAsync($"/api/v1/quotations/{quote.GetProperty("quotationId")}/process", null), HttpStatusCode.UnprocessableEntity);
        Assert.True(error.TryGetProperty("code", out _)); Assert.True(error.TryGetProperty("traceId", out _));
        Assert.DoesNotContain("System.", error.GetRawText());
        var stored = await StoryFlow.Json(await f.Analyst.GetAsync($"/api/v1/quotations/{quote.GetProperty("quotationId")}"));
        Assert.Equal("Rejected", stored.GetProperty("status").GetString());
    }

    // US05/E1-E2: corrección trazable y confirmación mantienen responsables y fechas reales.
    [Fact, Trait("Story", "US05"), Trait("Scenario", "E1"), Trait("Scenario", "E2")]
    public async Task CorrectedFieldRetainsOriginalReasonActorAndVerification()
    {
        using var f = new StoryFlow(); await f.Create(true); var q = await f.Quote("normal.pdf", false);
        var id = q.GetProperty("quotationId").GetGuid();
        var field = q.GetProperty("fields").EnumerateArray().Single(v => v.GetProperty("fieldPath").GetString() == "lines[0].unitPrice");
        using var response = await f.Analyst.PutAsJsonAsync($"/api/v1/quotations/{id}/fields/{field.GetProperty("fieldId")}", new { value = "4.25", reason = "Verified against PDF", expectedVersion = q.GetProperty("version").GetInt64() });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        q = await StoryFlow.Json(await f.Analyst.GetAsync($"/api/v1/quotations/{id}"));
        field = q.GetProperty("fields").EnumerateArray().Single(v => v.GetProperty("fieldPath").GetString() == "lines[0].unitPrice");
        Assert.Equal("4", field.GetProperty("originalValue").GetString()); Assert.Equal("4.25", field.GetProperty("currentValue").GetString());
        var correction = Assert.Single(field.GetProperty("corrections").EnumerateArray());
        Assert.Equal(f.AnalystId, correction.GetProperty("correctedBy").GetGuid());
        Assert.Equal("Verified against PDF", correction.GetProperty("reason").GetString());
        Assert.True(correction.GetProperty("correctedAt").GetDateTimeOffset() <= DateTimeOffset.UtcNow);
        using var confirmed = await f.Analyst.PostAsJsonAsync($"/api/v1/quotations/{id}/confirm", new { expectedVersion = q.GetProperty("version").GetInt64(), lineMappings = new[] { new { lineId = q.GetProperty("lines")[0].GetProperty("lineId").GetGuid(), requestedItemId = f.Request.GetProperty("items")[0].GetProperty("itemId").GetGuid().ToString() } } });
        Assert.Equal(HttpStatusCode.NoContent, confirmed.StatusCode);
        q = await StoryFlow.Json(await f.Analyst.GetAsync($"/api/v1/quotations/{id}"));
        Assert.Equal("Verified", q.GetProperty("status").GetString()); Assert.Equal(f.AnalystId, q.GetProperty("verifiedBy").GetGuid());
        Assert.True(q.GetProperty("verifiedAt").GetDateTimeOffset() <= DateTimeOffset.UtcNow);
    }

    // US06/E2: la API rechaza pesos inválidos y no publica una configuración.
    [Theory, InlineData(59), InlineData(-1), InlineData(101)]
    [Trait("Story", "US06"), Trait("Scenario", "E2")]
    public async Task InvalidCriteriaDoNotPersist(int weight)
    {
        using var f = new StoryFlow(); await f.Create(true);
        using var response = await f.Analyst.PostAsJsonAsync("/api/v1/evaluation-scenarios", new { requestId = f.RequestId.ToString(), criteria = f.Criteria(weight) });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        using var missing = await f.Analyst.GetAsync($"/api/v1/purchase-requests/{f.RequestId}/evaluation-scenario");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    // US06/E1-E3 y US08/E2: nuevas versiones conservan la anterior y bloquean aprobarla.
    [Fact, Trait("Story", "US06"), Trait("Scenario", "E1"), Trait("Scenario", "E3"), Trait("Story", "US08"), Trait("Scenario", "E2")]
    public async Task RevisedCriteriaPreserveHistoryAndInvalidateApproval()
    {
        using var f = new StoryFlow(); var (scenario, run) = await f.Ready();
        var next = await StoryFlow.Json(await f.Analyst.PostAsJsonAsync($"/api/v1/evaluation-scenarios/{scenario.GetProperty("scenarioId")}/versions", new { criteria = f.Criteria() }), HttpStatusCode.Created);
        Assert.Equal(2, next.GetProperty("version").GetInt32());
        var old = await StoryFlow.Json(await f.Analyst.GetAsync($"/api/v1/evaluation-scenarios/{scenario.GetProperty("scenarioId")}"));
        Assert.Equal("Superseded", old.GetProperty("status").GetString());
        var storedRun = await StoryFlow.Json(await f.Analyst.GetAsync($"/api/v1/simulations/{run.GetProperty("simulationRunId")}"));
        Assert.False(storedRun.GetProperty("isCurrent").GetBoolean());
        var rejected = await StoryFlow.Json(await f.Approve(run), HttpStatusCode.UnprocessableEntity);
        Assert.Equal("domain_rule_violation", rejected.GetProperty("code").GetString());
        using var noOrder = await f.Manager.GetAsync($"/api/v1/purchase-requests/{f.RequestId}/purchase-order"); Assert.Equal(HttpStatusCode.NotFound, noOrder.StatusCode);
    }

    // US07/E1-E3: ranking y contribuciones repetibles; oferta barata no conforme excluida.
    [Fact, Trait("Story", "US07"), Trait("Scenario", "E1"), Trait("Scenario", "E2"), Trait("Scenario", "E3")]
    public async Task SimulationExcludesCheapNonCompliantOfferAndIsRepeatable()
    {
        using var f = new StoryFlow(); var (scenario, first) = await f.Ready("normal.pdf", "expensive.pdf", "noncompliant.pdf");
        var rows = first.GetProperty("evaluations").EnumerateArray().ToArray();
        Assert.Equal(3, rows.Length); Assert.Equal(2, rows.Count(e => e.GetProperty("isEligible").GetBoolean()));
        var excluded = Assert.Single(rows, e => !e.GetProperty("isEligible").GetBoolean());
        Assert.Equal(JsonValueKind.Null, excluded.GetProperty("rank").ValueKind); Assert.NotEmpty(excluded.GetProperty("exclusionReasons").EnumerateArray());
        Assert.NotEqual(excluded.GetProperty("quotationId").GetString(), first.GetProperty("recommendation").GetProperty("quotationId").GetString());
        foreach (var row in rows.Where(e => e.GetProperty("isEligible").GetBoolean()))
        {
            var contributions = row.GetProperty("criterionResults").EnumerateArray();
            Assert.Equal(row.GetProperty("totalScore").GetDecimal(), contributions.Sum(c => c.GetProperty("weightedContribution").GetDecimal()));
            Assert.All(contributions, c => Assert.False(string.IsNullOrWhiteSpace(c.GetProperty("explanation").GetString())));
        }
        var second = await f.Simulate(scenario);
        Assert.Equal(first.GetProperty("inputFingerprint").GetString(), second.GetProperty("inputFingerprint").GetString());
        AssertEquivalentJson(first.GetProperty("evaluations"), second.GetProperty("evaluations"));
    }

    // US07/E1: una única cotización verificada no alcanza el mínimo para simular.
    [Fact, Trait("Story", "US07"), Trait("Scenario", "E1")]
    public async Task SimulationRequiresTwoVerifiedQuotations()
    {
        using var f = new StoryFlow(); await f.Create(true); await f.Quote("normal.pdf"); var scenario = await f.Scenario(); await f.Status("Evaluation");
        using var response = await f.Analyst.PostAsync($"/api/v1/evaluation-scenarios/{scenario.GetProperty("scenarioId")}/simulations", null);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    // US08/E1-E3 y TS04/E1: orden emitida se relee y la aprobación repetida no duplica filas.
    [Fact, Trait("Story", "US08"), Trait("Scenario", "E1"), Trait("Scenario", "E3"), Trait("Story", "TS04")]
    public async Task ApprovedOrderIsCompletePersistentAndIdempotent()
    {
        using var f = new StoryFlow(); var (_, run) = await f.Ready();
        var order = await StoryFlow.Json(await f.Approve(run), HttpStatusCode.Created);
        var retry = await StoryFlow.Json(await f.Approve(run));
        Assert.Equal(order.GetProperty("purchaseOrderId").GetGuid(), retry.GetProperty("purchaseOrderId").GetGuid());
        Assert.Equal(f.RequestId.ToString(), order.GetProperty("purchaseRequestId").GetString());
        Assert.Equal(4000m, order.GetProperty("total").GetDecimal()); Assert.Equal("PEN", order.GetProperty("currency").GetString());
        Assert.Equal("Almacén de prueba", order.GetProperty("deliveryDestination").GetString());
        Assert.Equal(1000m, order.GetProperty("lines")[0].GetProperty("quantity").GetDecimal());
        var fetched = await StoryFlow.Json(await f.Manager.GetAsync($"/api/v1/purchase-orders/{order.GetProperty("purchaseOrderId")}"));
        AssertEquivalentJson(order, fetched);
        using var scope = f.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PurchaseOrderingDbContext>();
        Assert.Equal(1, await db.PurchaseOrders.CountAsync(o => o.SourceDecision.PurchaseRequestId == f.RequestId.ToString()));
    }

    // US12/E1-E3: operaciones generan eventos reales; consulta autorizada es solo lectura.
    [Fact, Trait("Story", "US12"), Trait("Scenario", "E1"), Trait("Scenario", "E2"), Trait("Scenario", "E3")]
    public async Task AuditRecordsOperationsAndRejectsAlteration()
    {
        using var f = new StoryFlow(); await f.Create(true); await f.Status("Cancelled");
        var path = $"/api/v1/audit/PurchaseRequest/{f.RequestId}";
        var events = await StoryFlow.Json(await f.Manager.GetAsync(path));
        Assert.True(events.GetArrayLength() >= 4);
        Assert.All(events.EnumerateArray(), e => { Assert.Equal(f.RequestId, e.GetProperty("entityId").GetGuid()); Assert.NotEqual(Guid.Empty, e.GetProperty("actorId").GetGuid()); Assert.False(string.IsNullOrWhiteSpace(e.GetProperty("action").GetString())); });
        Assert.Equal(events.EnumerateArray().Select(e => e.GetProperty("occurredAt").GetDateTimeOffset()).Order(), events.EnumerateArray().Select(e => e.GetProperty("occurredAt").GetDateTimeOffset()));
        using var forbidden = await f.Analyst.GetAsync(path); Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        using var deleted = await f.Manager.DeleteAsync(path); Assert.Equal(HttpStatusCode.MethodNotAllowed, deleted.StatusCode);
        using var changed = await f.Manager.PutAsJsonAsync(path, new { reason = "tamper" }); Assert.Equal(HttpStatusCode.MethodNotAllowed, changed.StatusCode);
        Assert.Equal(events.GetRawText(), (await StoryFlow.Json(await f.Manager.GetAsync(path))).GetRawText());
    }

    // US13/E1-E3: evaluación persistida, rechazo antes de entrega/duplicación e historial.
    [Fact, Trait("Story", "US13"), Trait("Scenario", "E1"), Trait("Scenario", "E2"), Trait("Scenario", "E3")]
    public async Task DeliveryEvaluationIsPersistedOnlyAfterDeliveryAndAggregated()
    {
        using var f = new StoryFlow(); var (_, run) = await f.Ready(); var order = await StoryFlow.Json(await f.Approve(run), HttpStatusCode.Created);
        var id = order.GetProperty("purchaseOrderId").GetGuid(); var path = $"/api/v1/purchase-orders/{id}/delivery-evaluation";
        var input = new { onTimeScore = 4, qualityScore = 5, observations = "Entrega conforme" };
        using var pending = await f.Analyst.PostAsJsonAsync(path, input); Assert.Equal(HttpStatusCode.UnprocessableEntity, pending.StatusCode);
        await StoryFlow.Json(await f.Analyst.PostAsync($"/api/v1/purchase-orders/{id}/delivery", null));
        using var invalid = await f.Analyst.PostAsJsonAsync(path, new { onTimeScore = 6, qualityScore = 5, observations = "" }); Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var evaluation = await StoryFlow.Json(await f.Analyst.PostAsJsonAsync(path, input), HttpStatusCode.Created);
        Assert.Equal(f.AnalystId, evaluation.GetProperty("evaluatedBy").GetGuid()); Assert.Equal(id, evaluation.GetProperty("purchaseOrderId").GetGuid());
        using var duplicate = await f.Analyst.PostAsJsonAsync(path, input); Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        var history = await StoryFlow.Json(await f.Analyst.GetAsync("/api/v1/suppliers/20123456789/performance"));
        Assert.Contains(history.GetProperty("evaluations").EnumerateArray(), e => e.GetProperty("purchaseOrderId").GetGuid() == id && e.GetProperty("observations").GetString() == "Entrega conforme");
        var rows = history.GetProperty("evaluations").EnumerateArray().ToArray();
        Assert.Equal(rows.Length, history.GetProperty("evaluationCount").GetInt32());
        Assert.Equal(rows.Average(e => e.GetProperty("onTimeScore").GetDecimal()), history.GetProperty("averageOnTimeScore").GetDecimal());
    }

    // TS03/E1-E2: tasa controlada se conserva en PostgreSQL y JSON con importes originales.
    [Fact, Trait("Story", "TS03"), Trait("Scenario", "E1"), Trait("Scenario", "E2")]
    public async Task CurrencyConversionRetainsRateAndOriginalAmountsAfterReload()
    {
        using var f = new StoryFlow(); var (_, run) = await f.Ready("usd.pdf", "normal.pdf");
        var usd = Assert.Single(run.GetProperty("evaluations").EnumerateArray(), e => e.GetProperty("originalCurrency").GetString() == "USD");
        Assert.Equal(1000m, usd.GetProperty("originalTotal").GetDecimal()); Assert.Equal(3500m, usd.GetProperty("comparisonTotal").GetDecimal());
        Assert.Equal("PEN", usd.GetProperty("comparisonCurrency").GetString()); Assert.True(usd.GetProperty("conversionApplied").GetBoolean());
        var trace = run.GetProperty("exchangeRate"); Assert.Equal(3.5m, trace.GetProperty("rate").GetDecimal()); Assert.Equal("USD", trace.GetProperty("sourceCurrency").GetString());
        Assert.Contains("SUNAT", trace.GetProperty("source").GetString()); Assert.True(trace.TryGetProperty("retrievedAt", out _)); Assert.True(trace.TryGetProperty("publishedOn", out _));
        var calls = f.Factory.ExchangeRate.Calls; f.Factory.ExchangeRate.Unavailable = true;
        var stored = await StoryFlow.Json(await f.Manager.GetAsync($"/api/v1/simulations/{run.GetProperty("simulationRunId")}"));
        AssertEquivalentJson(trace, stored.GetProperty("exchangeRate")); Assert.Equal(calls, f.Factory.ExchangeRate.Calls);
    }

    // TS03/E3: sin fuente vigente no se crea un resultado ni se aplica una tasa anterior.
    [Fact, Trait("Story", "TS03"), Trait("Scenario", "E3")]
    public async Task UnavailableExchangeRateDoesNotPersistSimulation()
    {
        using var f = new StoryFlow(); await f.Create(true); await f.Quote("usd.pdf"); await f.Quote("normal.pdf"); var scenario = await f.Scenario(); await f.Status("Evaluation"); f.Factory.ExchangeRate.Unavailable = true;
        var error = await StoryFlow.Json(await f.Analyst.PostAsync($"/api/v1/evaluation-scenarios/{scenario.GetProperty("scenarioId")}/simulations", null), HttpStatusCode.ServiceUnavailable);
        Assert.True(error.TryGetProperty("code", out _));
        using var scope = f.Factory.Services.CreateScope(); Assert.False(await scope.ServiceProvider.GetRequiredService<EvaluationSimulationDbContext>().SimulationRuns.AnyAsync(r => r.RequestSnapshot.RequestId == f.RequestId.ToString()));
    }

    // US09/E1-E3 y TS02/E1: registro y aprobación reales, hash protegido y firma JWT validada.
    [Fact, Trait("Story", "US09"), Trait("Scenario", "E1"), Trait("Scenario", "E2"), Trait("Scenario", "E3"), Trait("Story", "TS02")]
    public async Task RealRegistrationLoginAndJwtProtectCredentials()
    {
        using var f = new StoryFlow(); using var anonymous = f.Factory.CreateClient();
        const string password = "Strong!Local2026_Test"; var email = $"test-{Guid.NewGuid():N}@example.test";
        var status = await StoryFlow.Json(await anonymous.GetAsync("/api/v1/iam/auth/registration-status"));
        var role = status.GetProperty("initialSetupRequired").GetBoolean() ? "PurchaseManager" : "PurchaseAnalyst";
        var registered = await StoryFlow.Json(await anonymous.PostAsJsonAsync("/api/v1/iam/auth/register", new { email, displayName = "Usuario de prueba", password, role }), HttpStatusCode.Created);
        if (registered.GetProperty("status").GetString() == "Pending")
            await StoryFlow.Json(await f.Manager.PostAsJsonAsync($"/api/v1/iam/registration-requests/{registered.GetProperty("userId")}/approve", new { role }));
        using var duplicate = await anonymous.PostAsJsonAsync("/api/v1/iam/auth/register", new { email, displayName = "Usuario de prueba", password, role }); Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        using var weak = await anonymous.PostAsJsonAsync("/api/v1/iam/auth/register", new { email = $"weak-{Guid.NewGuid():N}@example.test", displayName = "Usuario de prueba", password = "weak", role }); Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);
        using var wrong = await anonymous.PostAsJsonAsync("/api/v1/iam/auth/login", new { email, password = "Incorrect!Password2026" }); Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        var session = await StoryFlow.Json(await anonymous.PostAsJsonAsync("/api/v1/iam/auth/login", new { email, password }));
        var token = session.GetProperty("accessToken").GetString()!;
        var key = File.ReadAllText(Environment.GetEnvironmentVariable("SMARTQUOTE_JWT_KEY_FILE")!).Trim();
        var principal = new JwtSecurityTokenHandler().ValidateToken(token, new TokenValidationParameters { ValidIssuer = "SmartQuote", ValidAudience = "SmartQuote.Clients", IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), ValidateLifetime = true, ClockSkew = TimeSpan.Zero }, out var validated);
        Assert.NotNull(principal); var jwt = Assert.IsType<JwtSecurityToken>(validated); Assert.Equal(role, jwt.Claims.Single(c => c.Type == "role").Value);
        Assert.Equal(registered.GetProperty("userId").GetGuid().ToString(), jwt.Claims.Single(c => c.Type == "sub").Value);
        Assert.DoesNotContain(password, jwt.Payload.SerializeToJson()); Assert.DoesNotContain("SigningKey", jwt.Payload.SerializeToJson());
        using var scope = f.Factory.Services.CreateScope(); var account = await scope.ServiceProvider.GetRequiredService<IdentityAccessDbContext>().UserAccounts.SingleAsync(a => a.Email == email);
        Assert.NotEqual(password, account.PasswordHash);
        Assert.True(new SmartQuote.Modules.IdentityAccess.Infrastructure.Security.AspNetPasswordHasher().Verify(account.PasswordHash, password));
    }

    // TS02/E2-E3: acceso inválido/vencido y rol insuficiente no escriben en PostgreSQL.
    [Theory, InlineData("missing"), InlineData("invalid"), InlineData("expired"), InlineData("forbidden")]
    [Trait("Story", "TS02"), Trait("Scenario", "E2"), Trait("Scenario", "E3")]
    public async Task UnauthorizedWriteDoesNotChangeData(string kind)
    {
        using var f = new StoryFlow(); using var client = f.Factory.CreateClient(); using var scope = f.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SupplyRequestsDbContext>(); var count = await db.PurchaseRequests.CountAsync();
        if (kind != "missing") client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", kind == "invalid" ? "invalid" : f.Factory.Token(kind == "forbidden" ? "PurchaseAnalyst" : "ProductionSpecialist", expired: kind == "expired"));
        using var response = await client.PostAsJsonAsync("/api/v1/purchase-requests", StoryFlow.Payload());
        Assert.Equal(kind == "forbidden" ? HttpStatusCode.Forbidden : HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(count, await db.PurchaseRequests.CountAsync());
    }
}
