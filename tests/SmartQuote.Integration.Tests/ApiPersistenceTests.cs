using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SmartQuote.API.PurchaseOrdering.Domain.Model.Aggregates;
using SmartQuote.API.PurchaseOrdering.Domain.Model.ValueObjects;
using SmartQuote.API.PurchaseOrdering.Infrastructure.Persistence.EFC.Configuration;
using SmartQuote.API.Shared.Domain.Model.ValueObjects;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.Aggregates;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.Entities;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.ValueObjects;
using SmartQuote.Modules.EvaluationSimulation.Infrastructure.Persistence.EFC.Configuration;
using Xunit;

namespace SmartQuote.Integration.Tests;

[Collection("API integration")]
public sealed class ApiPersistenceTests : IClassFixture<SmartQuoteFactory>
{
    private readonly SmartQuoteFactory _factory;

    public ApiPersistenceTests(SmartQuoteFactory factory) => _factory = factory;

    [Fact]
    public async Task ProductionRequestIsPersistedInPostgreSql()
    {
        var production = Client("ProductionSpecialist");
        using var created = await production.PostAsJsonAsync("/api/v1/purchase-requests", RequestPayload());
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = await ReadJsonAsync(created);
        var requestId = body.GetProperty("requestId").GetGuid();

        using var fetched = await production.GetAsync($"/api/v1/purchase-requests/{requestId}");
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
        Assert.Equal("Submitted", (await ReadJsonAsync(fetched)).GetProperty("status").GetString());

        await using var connection = new NpgsqlConnection(_factory.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT status FROM supply_requests.purchase_requests WHERE id = @id", connection);
        command.Parameters.AddWithValue("id", requestId);
        Assert.Equal("Submitted", await command.ExecuteScalarAsync());
    }

    [Fact]
    public async Task MixedBatchPersistsValidPdfAndReturnsErrorForUnsupportedFile()
    {
        var requestId = await RequestAcceptingQuotationsAsync();
        var analyst = Client("PurchaseAnalyst");
        using var form = BatchForm(
            ("valid.pdf", "application/pdf", "%PDF-1.4\n% Integration fixture\n%%EOF\n"),
            ("invalid.txt", "text/plain", "This is not a PDF"));
        using var response = await analyst.PostAsync(
            $"/api/v1/purchase-requests/{requestId}/quotations/batch", form);
        Assert.Equal(HttpStatusCode.MultiStatus, response.StatusCode);
        var items = (await ReadJsonAsync(response)).EnumerateArray().ToArray();
        Assert.Equal(2, items.Length);
        var valid = Assert.Single(items, item => item.GetProperty("fileName").GetString() == "valid.pdf");
        var invalid = Assert.Single(items, item => item.GetProperty("fileName").GetString() == "invalid.txt");
        Assert.True(valid.GetProperty("wasCreated").GetBoolean());
        Assert.Equal("unsupported_media_type", invalid.GetProperty("errorCode").GetString());

        await using var connection = new NpgsqlConnection(_factory.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT id FROM quotation_intake.poultry_quotes WHERE purchase_request_id = @id", connection);
        command.Parameters.AddWithValue("id", requestId);
        Assert.Equal(valid.GetProperty("quotationId").GetGuid(), await command.ExecuteScalarAsync());
    }

    [Fact]
    public async Task DuplicatePdfsInBatchDoNotCreateDuplicateRows()
    {
        var requestId = await RequestAcceptingQuotationsAsync();
        var analyst = Client("PurchaseAnalyst");
        const string content = "%PDF-1.4\n% Duplicate integration fixture\n%%EOF\n";
        using var form = BatchForm(
            ("first.pdf", "application/pdf", content),
            ("second.pdf", "application/pdf", content));
        using var response = await analyst.PostAsync(
            $"/api/v1/purchase-requests/{requestId}/quotations/batch", form);
        Assert.Equal(HttpStatusCode.MultiStatus, response.StatusCode);
        var items = (await ReadJsonAsync(response)).EnumerateArray().ToArray();
        Assert.Equal(2, items.Length);
        Assert.Equal(items[0].GetProperty("quotationId").GetGuid(),
            items[1].GetProperty("quotationId").GetGuid());
        Assert.True(items[0].GetProperty("wasCreated").GetBoolean());
        Assert.False(items[1].GetProperty("wasCreated").GetBoolean());

        await using var connection = new NpgsqlConnection(_factory.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM quotation_intake.poultry_quotes WHERE purchase_request_id = @id", connection);
        command.Parameters.AddWithValue("id", requestId);
        Assert.Equal(1L, await command.ExecuteScalarAsync());
    }

    [Fact]
    public async Task AuthorizedPurchaseOrderLookupReadsPersistedOrder()
    {
        var order = PurchaseOrder.Create(
            new OrderNumber($"TS04-{Guid.NewGuid():N}"),
            new ApprovedPurchaseDecision(
                Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString(),
                new SupplierSnapshot("ts04-supplier", "Persisted TS04 supplier", "20123456789"),
                "PEN",
                [new ApprovedPurchaseLine(Guid.NewGuid().ToString(), Guid.NewGuid().ToString(),
                    "Poultry supply", 3m, "unit", 7m)],
                DeliveryTerms.Create(2, "Warehouse delivery", "Main warehouse"),
                new string('b', 64)),
            new Approval(new UserId(Guid.NewGuid()), DateTimeOffset.UtcNow, Guid.NewGuid().ToString()));

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<PurchaseOrderingDbContext>();
            database.PurchaseOrders.Add(order);
            await database.SaveChangesAsync();
        }

        using var manager = Client("PurchaseManager");
        using var response = await manager.GetAsync($"/api/v1/purchase-orders/{order.Id.Value}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var resource = await ReadJsonAsync(response);
        Assert.Equal(order.Id.Value, resource.GetProperty("purchaseOrderId").GetGuid());
        Assert.Equal(order.OrderNumber.Value, resource.GetProperty("orderNumber").GetString());
        Assert.Equal("Persisted TS04 supplier", resource.GetProperty("supplierBusinessName").GetString());
        Assert.Equal(21m, resource.GetProperty("total").GetDecimal());
        Assert.Single(resource.GetProperty("lines").EnumerateArray());
    }

    [Fact]
    public async Task MetricsWithoutOrdersMarkBothIndicatorsUnavailable()
    {
        using var manager = Client("PurchaseManager");
        using var response = await manager.GetAsync("/api/v1/purchasing-metrics?from=2099-01-01&to=2099-01-31");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal(0, body.GetProperty("orderCount").GetInt32());
        Assert.Equal(0, body.GetProperty("timeSampleCount").GetInt32());
        Assert.Equal(0, body.GetProperty("savingsSampleCount").GetInt32());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("averageProcessingHours").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("comparativeSavings").ValueKind);
    }

    [Fact]
    public async Task MetricsRequireManagerAndValidPeriod()
    {
        using var analyst = Client("PurchaseAnalyst");
        using var forbidden = await analyst.GetAsync("/api/v1/purchasing-metrics?from=2099-01-01&to=2099-01-31");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        using var manager = Client("PurchaseManager");
        using var invalid = await manager.GetAsync("/api/v1/purchasing-metrics?from=2099-02-01&to=2099-01-01");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task MetricsCountPersistedRequestToOrderTimeWithoutInventingSavings()
    {
        var day = DateOnly.FromDateTime(DateTime.UtcNow);
        var path = $"/api/v1/purchasing-metrics?from={day:yyyy-MM-dd}&to={day:yyyy-MM-dd}";
        using var manager = Client("PurchaseManager");
        using var beforeResponse = await manager.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, beforeResponse.StatusCode);
        var before = await ReadJsonAsync(beforeResponse);

        using var production = Client("ProductionSpecialist");
        using var created = await production.PostAsJsonAsync("/api/v1/purchase-requests", RequestPayload());
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var requestId = (await ReadJsonAsync(created)).GetProperty("requestId").GetGuid();

        var order = PurchaseOrder.Create(
            new OrderNumber($"US16-{Guid.NewGuid():N}"),
            new ApprovedPurchaseDecision(
                Guid.NewGuid().ToString(), requestId.ToString(), Guid.NewGuid().ToString(),
                new SupplierSnapshot("us16-supplier", "Metrics test supplier", "20123456789"),
                "PEN",
                [new ApprovedPurchaseLine(Guid.NewGuid().ToString(), Guid.NewGuid().ToString(),
                    "Poultry supply", 1m, "unit", 10m)],
                DeliveryTerms.Create(2, "Warehouse delivery", "Main warehouse"),
                new string('c', 64)),
            new Approval(new UserId(Guid.NewGuid()), DateTimeOffset.UtcNow, Guid.NewGuid().ToString()));
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<PurchaseOrderingDbContext>();
            database.PurchaseOrders.Add(order);
            await database.SaveChangesAsync();
        }

        using var response = await manager.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.True(body.GetProperty("timeSampleCount").GetInt32() >= 1);
        Assert.True(body.GetProperty("averageProcessingHours").GetDecimal() >= 0);
        Assert.Equal(before.GetProperty("savingsSampleCount").GetInt32(),
            body.GetProperty("savingsSampleCount").GetInt32());
        Assert.Equal(before.GetProperty("comparativeSavings").ToString(),
            body.GetProperty("comparativeSavings").ToString());
    }

    [Fact]
    public async Task MetricsReadPersistedSimulationForComparativeSavings()
    {
        var day = DateOnly.FromDateTime(DateTime.UtcNow);
        var path = $"/api/v1/purchasing-metrics?from={day:yyyy-MM-dd}&to={day:yyyy-MM-dd}";
        using var manager = Client("PurchaseManager");
        using var beforeResponse = await manager.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, beforeResponse.StatusCode);
        var before = await ReadJsonAsync(beforeResponse);
        var previousSavings = before.GetProperty("comparativeSavings").ValueKind == JsonValueKind.Null
            ? 0m : before.GetProperty("comparativeSavings").GetDecimal();
        var previousCount = before.GetProperty("savingsSampleCount").GetInt32();

        using var production = Client("ProductionSpecialist");
        using var created = await production.PostAsJsonAsync("/api/v1/purchase-requests", RequestPayload());
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var requestId = (await ReadJsonAsync(created)).GetProperty("requestId").GetGuid();

        var selectedId = Guid.NewGuid().ToString();
        var alternativeId = Guid.NewGuid().ToString();
        var scenario = EvaluationScenario.Create(requestId.ToString(), new UserId(Guid.NewGuid()));
        var fingerprint = InputFingerprint.FromParts(Guid.NewGuid().ToString());
        var run = SimulationRun.Create(scenario.Id, 1, fingerprint,
            new RequestEvaluationSnapshot(requestId.ToString(), 1, day.AddDays(7), "High", [], DateTimeOffset.UtcNow),
            [Snapshot(selectedId, 80m), Snapshot(alternativeId, 100m)]);
        run.AddEvaluation(QuotationEvaluation.Create(selectedId));
        run.AddEvaluation(QuotationEvaluation.Create(alternativeId));

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<EvaluationSimulationDbContext>();
            database.EvaluationScenarios.Add(scenario);
            database.SimulationRuns.Add(run);
            await database.SaveChangesAsync();
        }

        var order = PurchaseOrder.Create(new OrderNumber($"US16-{Guid.NewGuid():N}"),
            new ApprovedPurchaseDecision(run.Id.Value.ToString(), requestId.ToString(), selectedId,
                new SupplierSnapshot("us16-selected", "Selected supplier", "20123456789"), "PEN",
                [new ApprovedPurchaseLine(Guid.NewGuid().ToString(), Guid.NewGuid().ToString(),
                    "Poultry supply", 1m, "unit", 80m)],
                DeliveryTerms.Create(2, "Warehouse delivery", "Main warehouse"), fingerprint.Value),
            new Approval(new UserId(Guid.NewGuid()), DateTimeOffset.UtcNow, Guid.NewGuid().ToString()));
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<PurchaseOrderingDbContext>();
            database.PurchaseOrders.Add(order);
            await database.SaveChangesAsync();
        }

        using var response = await manager.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal(previousCount + 1, body.GetProperty("savingsSampleCount").GetInt32());
        Assert.Equal(previousSavings + 20m, body.GetProperty("comparativeSavings").GetDecimal());
    }

    private static QuotationEvaluationSnapshot Snapshot(string quotationId, decimal price) =>
        new(quotationId, 1, "supplier", "Supplier", "20123456789", "PEN", 2,
            DateTimeOffset.UtcNow,
            [new QuotationLineSnapshotData(null, null, 1, "Poultry supply", 1m, "unit", price, [])],
            DateTimeOffset.UtcNow);

    private HttpClient Client(string role)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _factory.Token(role));
        return client;
    }

    private async Task<Guid> RequestAcceptingQuotationsAsync()
    {
        var production = Client("ProductionSpecialist");
        var analyst = Client("PurchaseAnalyst");
        using var created = await production.PostAsJsonAsync("/api/v1/purchase-requests", RequestPayload());
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = await ReadJsonAsync(created);
        var requestId = body.GetProperty("requestId").GetGuid();
        var version = body.GetProperty("version").GetInt64();
        foreach (var nextStatus in new[] { "UnderReview", "QuotationCollection" })
        {
            using var changed = await analyst.PutAsJsonAsync(
                $"/api/v1/purchase-requests/{requestId}/status",
                new { nextStatus, reason = "Integration test", expectedVersion = version });
            Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);
            using var fetched = await analyst.GetAsync($"/api/v1/purchase-requests/{requestId}");
            Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
            body = await ReadJsonAsync(fetched);
            Assert.Equal(nextStatus, body.GetProperty("status").GetString());
            version = body.GetProperty("version").GetInt64();
        }
        return requestId;
    }

    private static object RequestPayload() => new
    {
        requiredDate = DateTime.UtcNow.AddDays(7).ToString("yyyy-MM-dd"),
        priority = "High",
        items = new[] { new
        {
            description = $"Integration test supply {Guid.NewGuid():N}",
            quantity = 1,
            unitOfMeasure = "unit",
            requirements = new[] { new
            {
                name = "documentReference", @operator = "Contains",
                expectedValue = "a", unitOfMeasure = "", isMandatory = true
            }}
        }}
    };

    private static MultipartFormDataContent BatchForm(
        params (string FileName, string ContentType, string Text)[] files)
    {
        var form = new MultipartFormDataContent();
        form.Add(new StringContent($"integration-{Guid.NewGuid():N}"), "supplierId");
        form.Add(new StringContent("Integration Test Supplier"), "supplierBusinessName");
        form.Add(new StringContent("TEST-INTEGRATION"), "supplierTaxIdentifier");
        foreach (var (fileName, contentType, text) in files)
        {
            var content = new ByteArrayContent(Encoding.ASCII.GetBytes(text));
            content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            form.Add(content, "files", fileName);
        }
        return form;
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }
}

public sealed class SmartQuoteFactory : WebApplicationFactory<Program>
{
    public string ConnectionString { get; } =
        Environment.GetEnvironmentVariable("SMARTQUOTE_TEST_CONNECTION")
        ?? throw new InvalidOperationException("SMARTQUOTE_TEST_CONNECTION is required.");
    private readonly string _signingKey = File.ReadAllText(
        Environment.GetEnvironmentVariable("SMARTQUOTE_JWT_KEY_FILE")
        ?? throw new InvalidOperationException("SMARTQUOTE_JWT_KEY_FILE is required."))
        .Trim();

    public SmartQuoteFactory()
    {
        // Program reads these settings before WebApplicationFactory's host callbacks run.
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", ConnectionString);
        Environment.SetEnvironmentVariable("Jwt__SigningKey", _signingKey);
        Environment.SetEnvironmentVariable("Jwt__Issuer", "SmartQuote");
        Environment.SetEnvironmentVariable("Jwt__Audience", "SmartQuote.Clients");
        Environment.SetEnvironmentVariable("Database__ApplyMigrations", "true");
        Environment.SetEnvironmentVariable("AI__Provider", "Stub");
        Environment.SetEnvironmentVariable("Logging__EventLog__LogLevel__Default", "None");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = ConnectionString,
                ["Jwt:SigningKey"] = _signingKey,
                ["Jwt:Issuer"] = "SmartQuote",
                ["Jwt:Audience"] = "SmartQuote.Clients",
                ["Database:ApplyMigrations"] = "true",
                ["AI:Provider"] = "Stub",
                ["Logging:EventLog:LogLevel:Default"] = "None"
            }));
    }

    public string Token(string role)
    {
        static string Encode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=')
            .Replace('+', '-').Replace('/', '_');
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var header = Encode(Encoding.UTF8.GetBytes("{\"alg\":\"HS256\",\"typ\":\"JWT\"}"));
        var payload = Encode(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["sub"] = Guid.NewGuid().ToString(), ["role"] = role,
            ["iss"] = "SmartQuote", ["aud"] = "SmartQuote.Clients",
            ["iat"] = now, ["nbf"] = now - 5, ["exp"] = now + 3600
        })));
        var message = $"{header}.{payload}";
        var signature = Encode(HMACSHA256.HashData(Encoding.UTF8.GetBytes(_signingKey),
            Encoding.UTF8.GetBytes(message)));
        return $"{message}.{signature}";
    }
}
