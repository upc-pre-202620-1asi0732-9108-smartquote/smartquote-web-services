using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Reqnroll;
using Xunit;

namespace SmartQuote.Bdd.Tests.Steps;

[Binding]
public sealed class PurchaseFlowSteps
{
    private readonly HttpClient _http = new()
    {
        BaseAddress = new Uri(Environment.GetEnvironmentVariable("SMARTQUOTE_API_URL")
            ?? "http://127.0.0.1:5088")
    };
    private readonly string _signingKey = File.ReadAllText(
        Environment.GetEnvironmentVariable("SMARTQUOTE_JWT_KEY_FILE")
        ?? throw new InvalidOperationException("SMARTQUOTE_JWT_KEY_FILE is required for local BDD tests."))
        .Trim();
    private string _role = "ProductionSpecialist";
    private readonly Dictionary<string, string> _tokens = new();
    private Guid _requestId;
    private HttpStatusCode _lastStatus;
    private JsonElement _lastBody;

    [Given("the SmartQuote API and PostgreSQL are available")]
    public async Task GivenApiAndDatabaseAreAvailable()
    {
        using var response = await _http.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Given("I have the (.*) role")]
    public void GivenIHaveRole(string role) => _role = role;

    [When("I register a complete purchase request")]
    public async Task WhenIRegisterACompleteRequest()
    {
        var response = await SendJsonAsync(HttpMethod.Post, "/api/v1/purchase-requests",
            NewRequest(), _role);
        _lastStatus = response.Status;
        _lastBody = response.Body;
        if (_lastStatus == HttpStatusCode.Created)
            _requestId = _lastBody.GetProperty("requestId").GetGuid();
    }

    [Then("the API responds with (.*)")]
    public void ThenApiRespondsWith(int status) => Assert.Equal(status, (int)_lastStatus);

    [Then("the request can be read back with Submitted status")]
    public async Task ThenRequestCanBeReadBack()
    {
        var response = await SendAsync(HttpMethod.Get,
            $"/api/v1/purchase-requests/{_requestId}", "ProductionSpecialist");
        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Equal("Submitted", response.Body.GetProperty("status").GetString());
        Assert.Equal(_requestId, response.Body.GetProperty("requestId").GetGuid());
    }

    [Given("a request has reached QuotationCollection status")]
    public async Task GivenRequestAcceptsQuotations()
    {
        var created = await SendJsonAsync(HttpMethod.Post, "/api/v1/purchase-requests",
            NewRequest(), "ProductionSpecialist");
        Assert.Equal(HttpStatusCode.Created, created.Status);
        _requestId = created.Body.GetProperty("requestId").GetGuid();
        var version = created.Body.GetProperty("version").GetInt64();
        foreach (var status in new[] { "UnderReview", "QuotationCollection" })
        {
            var changed = await SendJsonAsync(HttpMethod.Put,
                $"/api/v1/purchase-requests/{_requestId}/status",
                new { nextStatus = status, reason = "BDD scenario", expectedVersion = version },
                "PurchaseAnalyst");
            Assert.Equal(HttpStatusCode.NoContent, changed.Status);
            var current = await SendAsync(HttpMethod.Get,
                $"/api/v1/purchase-requests/{_requestId}", "PurchaseAnalyst");
            Assert.Equal(status, current.Body.GetProperty("status").GetString());
            version = current.Body.GetProperty("version").GetInt64();
        }
    }

    [When("an analyst uploads one PDF and one unsupported file in a batch")]
    public async Task WhenAnalystUploadsMixedBatch()
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent($"bdd-{Guid.NewGuid():N}"), "supplierId");
        form.Add(new StringContent("BDD Test Supplier"), "supplierBusinessName");
        form.Add(new StringContent("TEST-BDD"), "supplierTaxIdentifier");
        var pdf = new ByteArrayContent(Encoding.ASCII.GetBytes("%PDF-1.4\n% BDD fixture\n%%EOF\n"));
        pdf.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(pdf, "files", "valid.pdf");
        var invalid = new ByteArrayContent(Encoding.ASCII.GetBytes("not a PDF"));
        invalid.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(invalid, "files", "invalid.txt");
        using var request = Authorized(HttpMethod.Post,
            $"/api/v1/purchase-requests/{_requestId}/quotations/batch", "PurchaseAnalyst");
        request.Content = form;
        using var response = await _http.SendAsync(request);
        _lastStatus = response.StatusCode;
        _lastBody = await ReadJsonAsync(response);
    }

    [Then("the PDF is persisted while the unsupported file is rejected")]
    public async Task ThenOnlyThePdfPersists()
    {
        Assert.Equal(2, _lastBody.GetArrayLength());
        var items = _lastBody.EnumerateArray().ToArray();
        var valid = Assert.Single(items, item => item.GetProperty("fileName").GetString() == "valid.pdf");
        var invalid = Assert.Single(items, item => item.GetProperty("fileName").GetString() == "invalid.txt");
        var quotationId = valid.GetProperty("quotationId").GetGuid();
        Assert.True(valid.GetProperty("wasCreated").GetBoolean());
        Assert.Equal("unsupported_media_type", invalid.GetProperty("errorCode").GetString());
        var listed = await SendAsync(HttpMethod.Get,
            $"/api/v1/purchase-requests/{_requestId}/quotations", "PurchaseAnalyst");
        Assert.Equal(HttpStatusCode.OK, listed.Status);
        Assert.Equal(quotationId, Assert.Single(listed.Body.EnumerateArray())
            .GetProperty("quotationId").GetGuid());
    }

    private static object NewRequest() => new
    {
        requiredDate = DateTime.UtcNow.AddDays(7).ToString("yyyy-MM-dd"),
        priority = "High",
        items = new[] { new
        {
            description = $"BDD test supply {Guid.NewGuid():N}",
            quantity = 1,
            unitOfMeasure = "unit",
            requirements = new[] { new
            {
                name = "documentReference", @operator = "Contains", expectedValue = "a",
                unitOfMeasure = "", isMandatory = true
            }}
        }}
    };

    private async Task<(HttpStatusCode Status, JsonElement Body)> SendJsonAsync(
        HttpMethod method, string path, object payload, string role)
    {
        using var request = Authorized(method, path, role);
        request.Content = JsonContent.Create(payload);
        using var response = await _http.SendAsync(request);
        return (response.StatusCode, await ReadJsonAsync(response));
    }

    private async Task<(HttpStatusCode Status, JsonElement Body)> SendAsync(
        HttpMethod method, string path, string role)
    {
        using var request = Authorized(method, path, role);
        using var response = await _http.SendAsync(request);
        return (response.StatusCode, await ReadJsonAsync(response));
    }

    private HttpRequestMessage Authorized(HttpMethod method, string path, string role)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token(role));
        return request;
    }

    private string Token(string role)
    {
        if (_tokens.TryGetValue(role, out var existing)) return existing;
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
        var token = $"{message}.{signature}";
        _tokens[role] = token;
        return token;
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(text)) return default;
        using var document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }
}
