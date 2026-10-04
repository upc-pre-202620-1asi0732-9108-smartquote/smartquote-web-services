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

    [Theory]
    [InlineData("PurchaseManager")]
    [InlineData("PurchaseAnalyst")]
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

    [Fact]
    public async Task MissingTokenReturnsStructuredUnauthorizedResponse()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync(OrderPath(_factory.Order.Id.Value));
        await AssertProblem(response, HttpStatusCode.Unauthorized, "authentication_required");
        Assert.Equal("Bearer", response.Headers.WwwAuthenticate.Single().Scheme);
    }

    [Fact]
    public async Task InvalidTokenReturnsStructuredUnauthorizedResponse()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-token");
        using var response = await client.GetAsync(OrderPath(_factory.Order.Id.Value));
        await AssertProblem(response, HttpStatusCode.Unauthorized, "authentication_required");
    }

    [Fact]
    public async Task UnpermittedRoleReturnsStructuredForbiddenResponse()
    {
        using var client = Client("ProductionSpecialist");
        using var response = await client.GetAsync(OrderPath(_factory.Order.Id.Value));
        await AssertProblem(response, HttpStatusCode.Forbidden, "forbidden");
    }

    [Fact]
    public async Task AnalystCannotApproveAndGenerateOrder()
    {
        using var client = Client("PurchaseAnalyst");
        using var body = new StringContent("{}", Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(
            $"/api/v1/simulations/{Guid.NewGuid()}/quotations/{Guid.NewGuid()}/purchase-orders", body);
        await AssertProblem(response, HttpStatusCode.Forbidden, "forbidden");
    }

    [Fact]
    public async Task UnknownOrderReturnsStructuredNotFoundResponse()
    {
        using var client = Client("PurchaseManager");
        using var response = await client.GetAsync(OrderPath(Guid.NewGuid()));
        await AssertProblem(response, HttpStatusCode.NotFound, "resource_not_found");
    }

    [Fact]
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
    }

    private HttpClient Client(string role)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _factory.Token(role));
        return client;
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
