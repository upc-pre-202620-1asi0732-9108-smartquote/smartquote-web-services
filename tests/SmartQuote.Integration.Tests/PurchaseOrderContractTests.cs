using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SmartQuote.API.PurchaseOrdering.Application.Ports;
using SmartQuote.API.PurchaseOrdering.Domain.Model.Aggregates;
using SmartQuote.API.PurchaseOrdering.Domain.Model.ValueObjects;
using SmartQuote.API.Shared.Domain.Model.ValueObjects;
using Xunit;

namespace SmartQuote.Integration.Tests;

[CollectionDefinition("API integration", DisableParallelization = true)]
public sealed class ApiIntegrationCollection { }

[Collection("API integration")]
public sealed class PurchaseOrderContractTests(PurchaseOrderContractFactory factory)
    : IClassFixture<PurchaseOrderContractFactory>
{
    private readonly PurchaseOrderContractFactory _factory = factory;

    // TS04/E1: Authorized Consumer Receives Normalized Order; comprobación HTTP con repositorios preparados.
    [Theory]
    [InlineData("PurchaseManager")]
    [InlineData("PurchaseAnalyst")]
    [Trait("Story", "TS04"), Trait("Scenario", "E1"), Trait("Category", "Contract")]
    public async Task AuthorizedConsumerReceivesNormalizedOrder(string role)
    {
        using var client = Client(role);
        using var response = await client.GetAsync(OrderPath(_factory.Order.Id.Value));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var order = json.RootElement;
        Assert.Equal(_factory.Order.Id.Value, order.GetProperty("purchaseOrderId").GetGuid());
        Assert.Equal("SQ-TEST-001", order.GetProperty("orderNumber").GetString());
        Assert.Equal("Supplier for contract test", order.GetProperty("supplierBusinessName").GetString());
        Assert.Equal("PEN", order.GetProperty("currency").GetString());
        Assert.Equal("Issued", order.GetProperty("status").GetString());
        Assert.Equal(25m, order.GetProperty("total").GetDecimal());
        Assert.NotEqual(default, order.GetProperty("approvedAt").GetDateTimeOffset());
        Assert.NotEqual(default, order.GetProperty("createdAt").GetDateTimeOffset());
        var line = Assert.Single(order.GetProperty("lines").EnumerateArray());
        Assert.Equal("Poultry supply", line.GetProperty("description").GetString());
        Assert.Equal(5m, line.GetProperty("quantity").GetDecimal());
        Assert.Equal(5m, line.GetProperty("unitPrice").GetDecimal());
    }

    // TS02/E2: Missing Token Returns Structured Unauthorized Response; comprobación HTTP con repositorios preparados.
    [Fact]
    [Trait("Story", "TS02"), Trait("Scenario", "E2"), Trait("Category", "Contract")]
    public async Task MissingTokenReturnsStructuredUnauthorizedResponse()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync(OrderPath(_factory.Order.Id.Value));
        await AssertProblem(response, HttpStatusCode.Unauthorized, "authentication_required");
        Assert.Equal("Bearer", response.Headers.WwwAuthenticate.Single().Scheme);
    }

    // TS02/E2: Invalid Token Returns Structured Unauthorized Response; comprobación HTTP con repositorios preparados.
    [Fact]
    [Trait("Story", "TS02"), Trait("Scenario", "E2"), Trait("Category", "Contract")]
    public async Task InvalidTokenReturnsStructuredUnauthorizedResponse()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-token");
        using var response = await client.GetAsync(OrderPath(_factory.Order.Id.Value));
        await AssertProblem(response, HttpStatusCode.Unauthorized, "authentication_required");
    }

    // TS04/E2: Unpermitted Role Returns Structured Forbidden Response; comprobación HTTP con repositorios preparados.
    [Fact]
    [Trait("Story", "TS04"), Trait("Scenario", "E2"), Trait("Category", "Contract")]
    public async Task UnpermittedRoleReturnsStructuredForbiddenResponse()
    {
        using var client = Client("ProductionSpecialist");
        using var response = await client.GetAsync(OrderPath(_factory.Order.Id.Value));
        await AssertProblem(response, HttpStatusCode.Forbidden, "forbidden");
    }

    // TS02/E3: Analyst Cannot Approve And Generate Order; comprobación HTTP con repositorios preparados.
    [Fact]
    [Trait("Story", "TS02"), Trait("Scenario", "E3"), Trait("Category", "Contract")]
    public async Task AnalystCannotApproveAndGenerateOrder()
    {
        using var client = Client("PurchaseAnalyst");
        using var body = new StringContent("{}", Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(
            $"/api/v1/simulations/{Guid.NewGuid()}/quotations/{Guid.NewGuid()}/purchase-orders", body);
        await AssertProblem(response, HttpStatusCode.Forbidden, "forbidden");
    }

    // TS04/E2: Unknown Order Returns Structured Not Found Response; comprobación HTTP con repositorios preparados.
    [Fact]
    [Trait("Story", "TS04"), Trait("Scenario", "E2"), Trait("Category", "Contract")]
    public async Task UnknownOrderReturnsStructuredNotFoundResponse()
    {
        using var client = Client("PurchaseManager");
        using var response = await client.GetAsync(OrderPath(Guid.NewGuid()));
        await AssertProblem(response, HttpStatusCode.NotFound, "resource_not_found");
    }

    // TS04/E3: Open Api Documents Response And Errors; comprobación HTTP con repositorios preparados.
    [Fact]
    [Trait("Story", "TS04"), Trait("Scenario", "E3"), Trait("Category", "Contract")]
    public async Task OpenApiDocumentsResponseAndErrors()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var responses = json.RootElement.GetProperty("paths")
            .GetProperty("/api/v1/purchase-orders/{purchaseOrderId}")
            .GetProperty("get").GetProperty("responses");
        foreach (var status in new[] { "200", "401", "403", "404" })
            Assert.True(responses.TryGetProperty(status, out _), $"OpenAPI omits HTTP {status}.");
        // TS04/E3: también se verifican nombres y tipos, no solo los códigos HTTP.
        var schemaReference = responses.GetProperty("200").GetProperty("content")
            .GetProperty("application/json").GetProperty("schema").GetProperty("$ref").GetString()!;
        var schema = json.RootElement.GetProperty("components").GetProperty("schemas").GetProperty(schemaReference.Split('/').Last());
        var properties = schema.GetProperty("properties");
        foreach (var name in new[] { "purchaseOrderId", "orderNumber", "supplierBusinessName", "supplierTaxIdentifier", "currency", "status", "approvedAt", "createdAt" })
            Assert.Equal("string", properties.GetProperty(name).GetProperty("type").GetString());
        Assert.Equal("number", properties.GetProperty("total").GetProperty("type").GetString());
        Assert.Equal("array", properties.GetProperty("lines").GetProperty("type").GetString());
    }

    private HttpClient Client(string role)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _factory.Token(role));
        return client;
    }

    // US09/E3: Login Rate Limit Reports AWait Of At Most15Seconds; comprobación HTTP con repositorios preparados.
    [Fact]
    [Trait("Story", "US09"), Trait("Scenario", "E3"), Trait("Category", "Contract")]
    public async Task LoginRateLimitReportsAWaitOfAtMost15Seconds()
    {
        using var client = _factory.CreateClient();
        for (var attempt = 0; attempt < 10; attempt++)
        {
            using var invalidBody = new StringContent("{}", Encoding.UTF8, "application/json");
            using var invalidResponse = await client.PostAsync("/api/v1/iam/auth/login", invalidBody);
            Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);
        }
        using var body = new StringContent("{}", Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/api/v1/iam/auth/login", body);
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.InRange(int.Parse(response.Headers.GetValues("Retry-After").Single()), 1, 15);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("rate_limit_exceeded", json.RootElement.GetProperty("code").GetString());
        Assert.Contains("segundos", json.RootElement.GetProperty("detail").GetString());
    }

    // US13/E3: Supplier History Matches The Summary And Retains Traceability; comprobación HTTP con repositorios preparados.
    [Theory]
    [InlineData("PurchaseManager")]
    [InlineData("PurchaseAnalyst")]
    [Trait("Story", "US13"), Trait("Scenario", "E3"), Trait("Category", "Contract")]
    public async Task SupplierHistoryMatchesTheSummaryAndRetainsTraceability(string role)
    {
        using var client = Client(role);
        using var response = await client.GetAsync("/api/v1/suppliers/20123456789/performance");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var summary = json.RootElement;
        var history = summary.GetProperty("evaluations").EnumerateArray().ToArray();
        Assert.Equal(2, summary.GetProperty("evaluationCount").GetInt32());
        Assert.Equal(2, history.Length);
        Assert.Equal(3m, summary.GetProperty("averageOnTimeScore").GetDecimal());
        Assert.Equal(4m, summary.GetProperty("averageQualityScore").GetDecimal());
        Assert.Equal(3.5m, summary.GetProperty("overallScore").GetDecimal());
        Assert.Equal(summary.GetProperty("lastEvaluatedAt").GetDateTimeOffset(), history[0].GetProperty("evaluatedAt").GetDateTimeOffset());
        Assert.Equal(summary.GetProperty("firstEvaluatedAt").GetDateTimeOffset(), history[1].GetProperty("evaluatedAt").GetDateTimeOffset());
        Assert.Equal("Entrega completa", history[0].GetProperty("observations").GetString());
        foreach (var item in history)
        {
            Assert.NotEqual(Guid.Empty, item.GetProperty("deliveryEvaluationId").GetGuid());
            Assert.NotEqual(Guid.Empty, item.GetProperty("purchaseOrderId").GetGuid());
            Assert.NotEqual(Guid.Empty, item.GetProperty("evaluatedBy").GetGuid());
            Assert.Equal("20123456789", item.GetProperty("supplierTaxIdentifier").GetString());
        }
    }

    // US13/E3: Supplier Without Evaluations Has Empty History And No Invented Score; comprobación HTTP con repositorios preparados.
    [Fact]
    [Trait("Story", "US13"), Trait("Scenario", "E3"), Trait("Category", "Contract")]
    public async Task SupplierWithoutEvaluationsHasEmptyHistoryAndNoInventedScore()
    {
        using var client = Client("PurchaseAnalyst");
        using var response = await client.GetAsync("/api/v1/suppliers/20999999999/performance");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(0, json.RootElement.GetProperty("evaluationCount").GetInt32());
        Assert.Empty(json.RootElement.GetProperty("evaluations").EnumerateArray());
        foreach (var property in new[] { "overallScore", "averageOnTimeScore", "averageQualityScore", "firstEvaluatedAt", "lastEvaluatedAt" })
            Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty(property).ValueKind);
    }

    // US13/E3: Production Cannot Read Supplier Evaluations; comprobación HTTP con repositorios preparados.
    [Fact]
    [Trait("Story", "US13"), Trait("Scenario", "E3"), Trait("Category", "Contract")]
    public async Task ProductionCannotReadSupplierEvaluations()
    {
        using var client = Client("ProductionSpecialist");
        using var response = await client.GetAsync("/api/v1/suppliers/20123456789/performance");
        await AssertProblem(response, HttpStatusCode.Forbidden, "forbidden");
    }

    // US13/E2: Undelivered Order Cannot Be Evaluated; comprobación HTTP con repositorios preparados.
    [Fact]
    [Trait("Story", "US13"), Trait("Scenario", "E2"), Trait("Category", "Contract")]
    public async Task UndeliveredOrderCannotBeEvaluated()
    {
        using var client = Client("PurchaseAnalyst");
        using var body = new StringContent("{\"onTimeScore\":5,\"qualityScore\":4}", Encoding.UTF8, "application/json");
        using var response = await client.PostAsync($"/api/v1/purchase-orders/{_factory.Order.Id.Value}/delivery-evaluation", body);
        await AssertProblem(response, HttpStatusCode.UnprocessableEntity, "domain_rule_violation");
    }

    private static string OrderPath(Guid id) => $"/api/v1/purchase-orders/{id}";

    private static async Task AssertProblem(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Supplier for contract test", body);
        using var json = JsonDocument.Parse(body);
        Assert.Equal((int)status, json.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(code, json.RootElement.GetProperty("code").GetString());
    }
}

public sealed class PurchaseOrderContractFactory : WebApplicationFactory<Program>
{
    private const string SigningKey = "SmartQuote-contract-test-signing-key-2026";
    private readonly ContractOrderRepository _repository = new();

    public PurchaseOrder Order => _repository.Order;

    public PurchaseOrderContractFactory()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection",
            "Host=127.0.0.1;Port=1;Database=contract_test;Username=unused;Password=unused");
        Environment.SetEnvironmentVariable("Jwt__SigningKey", SigningKey);
        Environment.SetEnvironmentVariable("Jwt__Issuer", "SmartQuote");
        Environment.SetEnvironmentVariable("Jwt__Audience", "SmartQuote.Clients");
        Environment.SetEnvironmentVariable("Database__ApplyMigrations", "false");
        Environment.SetEnvironmentVariable("AI__Provider", "Stub");
        Environment.SetEnvironmentVariable("Logging__EventLog__LogLevel__Default", "None");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Host=127.0.0.1;Port=1;Database=contract_test;Username=unused;Password=unused",
                ["Jwt:SigningKey"] = SigningKey,
                ["Jwt:Issuer"] = "SmartQuote",
                ["Jwt:Audience"] = "SmartQuote.Clients",
                ["Database:ApplyMigrations"] = "false",
                ["AI:Provider"] = "Stub",
                ["Logging:EventLog:LogLevel:Default"] = "None"
            }));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPurchaseOrderRepository>();
            services.AddSingleton<IPurchaseOrderRepository>(_repository);
            services.RemoveAll<IDeliveryEvaluationRepository>();
            services.AddSingleton<IDeliveryEvaluationRepository>(new ContractEvaluationRepository());
        });
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
        var signature = Encode(HMACSHA256.HashData(Encoding.UTF8.GetBytes(SigningKey),
            Encoding.UTF8.GetBytes(message)));
        return $"{message}.{signature}";
    }

    private sealed class ContractEvaluationRepository : IDeliveryEvaluationRepository
    {
        private readonly IReadOnlyList<DeliveryEvaluation> _evaluations =
        [
            DeliveryEvaluation.Create(new PurchaseOrderId(Guid.NewGuid()), "20123456789", 4, 5,
                "Entrega completa", new UserId(Guid.NewGuid()), new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero)),
            DeliveryEvaluation.Create(new PurchaseOrderId(Guid.NewGuid()), "20123456789", 2, 3,
                null, new UserId(Guid.NewGuid()), new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero)),
            DeliveryEvaluation.Create(new PurchaseOrderId(Guid.NewGuid()), "20698765432", 1, 1,
                "Otro proveedor", new UserId(Guid.NewGuid()), new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero))
        ];

        public Task<IReadOnlyList<DeliveryEvaluation>> ListBySupplierAsync(string taxIdentifier, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DeliveryEvaluation>>(_evaluations.Where(item => item.SupplierTaxIdentifier == taxIdentifier).ToList());
        public Task<bool> ExistsForOrderAsync(PurchaseOrderId id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_evaluations.Any(item => item.PurchaseOrderId == id));
        public Task AddAsync(DeliveryEvaluation evaluation, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class ContractOrderRepository : IPurchaseOrderRepository
    {
        public PurchaseOrder Order { get; } = PurchaseOrder.Create(
            new OrderNumber("SQ-TEST-001"),
            new ApprovedPurchaseDecision(
                Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString(),
                new SupplierSnapshot("supplier-1", "Supplier for contract test", "20123456789"),
                "PEN",
                [new ApprovedPurchaseLine(Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), "Poultry supply", 5m, "unit", 5m)],
                DeliveryTerms.Create(3, "Warehouse delivery", "Main warehouse"),
                new string('a', 64)),
            new Approval(new UserId(Guid.NewGuid()), DateTimeOffset.UtcNow, "contract-test"));

        public Task<PurchaseOrder?> GetByIdAsync(PurchaseOrderId id, CancellationToken cancellationToken = default) =>
            Task.FromResult<PurchaseOrder?>(id.Value == Order.Id.Value ? Order : null);

        public Task AddAsync(PurchaseOrder order, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PurchaseOrder?> FindBySimulationAsync(string id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PurchaseOrder?> FindByIdempotencyKeyAsync(string id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PurchaseOrder?> FindByRequestAsync(string id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
