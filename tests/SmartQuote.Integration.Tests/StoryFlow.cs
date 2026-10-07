using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SmartQuote.API.Shared.Domain;
using SmartQuote.Modules.EvaluationSimulation.Application.Ports;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.ValueObjects;
using SmartQuote.Modules.QuotationIntake.Application.AgentContracts;
using SmartQuote.Modules.QuotationIntake.Application.Ports;
using Xunit;

namespace SmartQuote.Integration.Tests;

// Doble solo del servicio externo. Controladores, servicios, dominio y PostgreSQL son reales.
public sealed class StoryFactory : SmartQuoteFactory
{
    public TestExchangeRate ExchangeRate { get; } = new();
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IQuoteExtractionAgent>();
            services.AddSingleton<IQuoteExtractionAgent, EvidenceExtractionAgent>();
            services.RemoveAll<IExchangeRateProvider>();
            services.AddSingleton<IExchangeRateProvider>(ExchangeRate);
        });
    }
}

public sealed class TestExchangeRate : IExchangeRateProvider
{
    public bool Unavailable { get; set; }
    public int Calls { get; private set; }
    private ExchangeRateSnapshot? _snapshot;
    public Task<ExchangeRateSnapshot> GetUsdToPenAsync(DateOnly applicableDate, CancellationToken cancellationToken = default)
    {
        Calls++;
        if (Unavailable) throw new ExternalServiceUnavailableException("SUNAT unavailable in controlled test; no fallback.");
        return Task.FromResult(_snapshot ??= new ExchangeRateSnapshot("USD", "PEN", 3.5m, "V", applicableDate,
            "SUNAT - controlled HTTP boundary", DateTimeOffset.UtcNow));
    }
}

public sealed class EvidenceExtractionAgent : IQuoteExtractionAgent
{
    public Task<ExtractionResult> ExtractAsync(QuotationDocument document, PurchaseRequestReferenceData request, CancellationToken cancellationToken = default)
    {
        if (document.FileName.Contains("external-unavailable")) throw new HttpRequestException("Controlled external extraction outage.");
        if (document.FileName.Contains("unreadable")) throw new UnprocessableDocumentException("Controlled illegible document.");
        var usd = document.FileName.Contains("usd");
        var nonCompliant = document.FileName.Contains("noncompliant");
        var missing = document.FileName.Contains("missing");
        var low = document.FileName.Contains("low-confidence");
        var price = nonCompliant ? 1m : usd ? 1m : document.FileName.Contains("expensive") ? 4.5m : 4m;
        var currency = usd ? "USD" : "PEN";
        var validity = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));
        var supplier = "Supplier " + document.FileName;
        var fields = new List<ExtractedFieldResult>
        {
            Field("supplier.businessName", supplier), Field("supplier.taxIdentifier", "20123456789"),
            Field("validUntil", validity.ToString("yyyy-MM-dd")), Field("currency", currency),
            Field("deliveryLeadTimeDays", "3"), Field("lines[0].description", "Alimento balanceado"),
            Field("lines[0].quantity", "1000"), Field("lines[0].unitOfMeasure", "kg"),
            new("lines[0].unitPrice", missing ? null : price.ToString(CultureInfo.InvariantCulture),
                low ? 0.3m : 1m, 1, "Unit price in controlled extraction fixture", !missing),
            Field("lines[0].specifications[0].value", nonCompliant ? "18" : "21")
        };
        return Task.FromResult(new ExtractionResult(supplier, "20123456789", validity, currency, 3,
            [new ExtractedLineResult("Alimento balanceado", 1000m, "kg", missing ? null : price,
                [new ExtractedSpecificationResult("Proteína cruda", nonCompliant ? "18" : "21", "%")])], fields));
    }
    private static ExtractedFieldResult Field(string path, string value) => new(path, value, 1m, 1, "Controlled document evidence: " + value, true);
}

public sealed class StoryFlow : IDisposable
{
    public StoryFactory Factory { get; } = new();
    public Guid ProductionId { get; } = Guid.NewGuid();
    public Guid AnalystId { get; } = Guid.NewGuid();
    public Guid ManagerId { get; } = Guid.NewGuid();
    public HttpClient Production { get; }
    public HttpClient Analyst { get; }
    public HttpClient Manager { get; }
    public JsonElement Request { get; private set; }
    public Guid RequestId => Request.GetProperty("requestId").GetGuid();
    public StoryFlow()
    {
        Production = Client("ProductionSpecialist", ProductionId);
        Analyst = Client("PurchaseAnalyst", AnalystId);
        Manager = Client("PurchaseManager", ManagerId);
    }
    public HttpClient Client(string role, Guid? id = null)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Factory.Token(role, id));
        return client;
    }
    public static object Payload(bool mandatory = true, decimal quantity = 1000) => new
    {
        requiredDate = DateTime.UtcNow.AddDays(7).ToString("yyyy-MM-dd"), priority = "High",
        items = new[] { new { description = "Alimento balanceado", quantity, unitOfMeasure = "kg",
            requirements = new[] { new { name = "Proteína mínima", @operator = "GreaterThanOrEqual", expectedValue = "20", unitOfMeasure = "%", isMandatory = mandatory } } } }
    };
    public async Task Create(bool collecting = false)
    {
        Request = await Json(await Production.PostAsJsonAsync("/api/v1/purchase-requests", Payload()), HttpStatusCode.Created);
        if (collecting) { await Status("UnderReview"); await Status("QuotationCollection"); }
    }
    public async Task Status(string status)
    {
        using var changed = await Analyst.PutAsJsonAsync($"/api/v1/purchase-requests/{RequestId}/status",
            new { nextStatus = status, reason = "Story acceptance verification", expectedVersion = Request.GetProperty("version").GetInt64() });
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);
        Request = await Json(await Production.GetAsync($"/api/v1/purchase-requests/{RequestId}"));
    }
    public static MultipartFormDataContent File(string name, string field = "file", string type = "application/pdf", byte[]? content = null)
    {
        var form = new MultipartFormDataContent();
        var bytes = new ByteArrayContent(content ?? Encoding.ASCII.GetBytes("%PDF-1.4\n% " + name + Guid.NewGuid() + "\n%%EOF\n"));
        bytes.Headers.ContentType = new MediaTypeHeaderValue(type);
        form.Add(bytes, field, name);
        return form;
    }
    public async Task<JsonElement> Quote(string name, bool verify = true)
    {
        using var form = File(name);
        var uploaded = await Json(await Analyst.PostAsync($"/api/v1/purchase-requests/{RequestId}/quotations", form), HttpStatusCode.Created);
        return await Process(uploaded.GetProperty("quotationId").GetGuid(), verify);
    }
    public async Task<JsonElement> Process(Guid id, bool verify = true)
    {
        var quote = await Json(await Analyst.PostAsync($"/api/v1/quotations/{id}/process", null));
        if (verify)
        {
            using var response = await Analyst.PostAsJsonAsync($"/api/v1/quotations/{id}/confirm", new
            {
                expectedVersion = quote.GetProperty("version").GetInt64(),
                lineMappings = quote.GetProperty("lines").EnumerateArray().Select(line => new { lineId = line.GetProperty("lineId").GetGuid(), requestedItemId = Request.GetProperty("items")[0].GetProperty("itemId").GetGuid().ToString() })
            });
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            quote = await Json(await Analyst.GetAsync($"/api/v1/quotations/{id}"));
        }
        return quote;
    }
    public object[] Criteria(decimal priceWeight = 60) =>
    [
        new { name = "Proteína mínima", targetField = Request.GetProperty("items")[0].GetProperty("requirements")[0].GetProperty("requirementId").GetGuid().ToString(), category = "TechnicalCompliance", mode = "Mandatory", @operator = "GreaterThanOrEqual", expectedValue = "20", unitOfMeasure = "%", weight = 0m, displayOrder = 1 },
        new { name = "Precio", targetField = "totalPrice", category = "Price", mode = "Weighted", @operator = "LessThanOrEqual", expectedValue = "999999", unitOfMeasure = "PEN", weight = priceWeight, displayOrder = 2 },
        new { name = "Plazo", targetField = "deliveryLeadTimeDays", category = "DeliveryTime", mode = "Weighted", @operator = "LessThanOrEqual", expectedValue = "30", unitOfMeasure = "days", weight = 40m, displayOrder = 3 }
    ];
    public async Task<JsonElement> Scenario() => await Json(await Analyst.PostAsJsonAsync("/api/v1/evaluation-scenarios", new { requestId = RequestId.ToString(), criteria = Criteria() }), HttpStatusCode.Created);
    public async Task<JsonElement> Simulate(JsonElement scenario) => await Json(await Analyst.PostAsync($"/api/v1/evaluation-scenarios/{scenario.GetProperty("scenarioId").GetGuid()}/simulations", null), HttpStatusCode.Created, HttpStatusCode.OK);
    public async Task<(JsonElement Scenario, JsonElement Run)> Ready(params string[] names)
    {
        await Create(true);
        foreach (var name in names.Length == 0 ? new[] { "normal.pdf", "expensive.pdf" } : names) await Quote(name);
        var scenario = await Scenario();
        await Status("Evaluation");
        return (scenario, await Simulate(scenario));
    }
    public async Task<HttpResponseMessage> Approve(JsonElement run) => await Manager.PostAsJsonAsync(
        $"/api/v1/simulations/{run.GetProperty("simulationRunId").GetGuid()}/quotations/{run.GetProperty("recommendation").GetProperty("quotationId").GetString()}/purchase-orders",
        new { deliveryConditions = "Recepción de 8 a 16", deliveryDestination = "Almacén de prueba" });
    public static async Task<JsonElement> Json(HttpResponseMessage response, params HttpStatusCode[] statuses)
    {
        using (response)
        {
            var text = await response.Content.ReadAsStringAsync();
            Assert.True((statuses.Length == 0 ? new[] { HttpStatusCode.OK } : statuses).Contains(response.StatusCode), $"Unexpected {(int)response.StatusCode}: {text}");
            using var document = JsonDocument.Parse(text);
            return document.RootElement.Clone();
        }
    }
    public void Dispose() { Production.Dispose(); Analyst.Dispose(); Manager.Dispose(); Factory.Dispose(); }
}
