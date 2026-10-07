using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using SmartQuote.API.Shared.Domain;
using SmartQuote.API.Shared.Domain.Model.ValueObjects;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.ValueObjects;
using SmartQuote.Modules.EvaluationSimulation.Infrastructure.ExchangeRates;
using Xunit;

namespace SmartQuote.Domain.Tests;

public class ExchangeRateTests
{
    // TS03/E1: Snapshot Converts Usd And Preserves Pen; comprobación aislada.
    [Fact]
    [Trait("Story", "TS03"), Trait("Scenario", "E1"), Trait("Category", "Unit")]
    public void SnapshotConvertsUsdAndPreservesPen()
    {
        var snapshot = new ExchangeRateSnapshot(
            "USD", "PEN", 3.44m, "V", new DateOnly(2026, 9, 29),
            "SUNAT - Consulta de Tipo de Cambio", DateTimeOffset.UtcNow);

        Assert.Equal(new Money(344m, "PEN"), snapshot.Convert(new Money(100m, "USD")));
        Assert.Equal(new Money(100m, "PEN"), snapshot.Convert(new Money(100m, "PEN")));
    }

    // TS03/E2: Sunat Provider Uses Exact Sale Rate And Browser Identification Headers; comprobación aislada.
    [Fact]
    [Trait("Story", "TS03"), Trait("Scenario", "E2"), Trait("Category", "Unit")]
    public async Task SunatProviderUsesExactSaleRateAndBrowserIdentificationHeaders()
    {
        var handler = new RecordingHandler("""
            [
              { "fecPublica": "29/09/2026", "valTipo": "3.432", "codTipo": "C" },
              { "fecPublica": "29/09/2026", "valTipo": "3.440", "codTipo": "V" }
            ]
            """);
        var provider = CreateProvider(handler);

        var result = await provider.GetUsdToPenAsync(new DateOnly(2026, 9, 29));

        Assert.Equal(3.440m, result.Rate);
        Assert.Equal("V", result.RateType);
        Assert.Equal(new DateOnly(2026, 9, 29), result.PublishedOn);
        Assert.Contains("Mozilla/5.0", handler.UserAgent);
        Assert.Equal("https://e-consulta.sunat.gob.pe", handler.Origin);
        Assert.Contains("\"mes\":8", handler.Body);
    }

    // TS03/E3: Sunat Provider Does Not Substitute Previous Rate; comprobación aislada.
    [Fact]
    [Trait("Story", "TS03"), Trait("Scenario", "E3"), Trait("Category", "Unit")]
    public async Task SunatProviderDoesNotSubstitutePreviousRate()
    {
        var handler = new RecordingHandler("""
            [{ "fecPublica": "28/09/2026", "valTipo": "3.425", "codTipo": "V" }]
            """);
        var provider = CreateProvider(handler);

        var exception = await Assert.ThrowsAsync<ExternalServiceUnavailableException>(
            () => provider.GetUsdToPenAsync(new DateOnly(2026, 9, 29)));

        Assert.Contains("No previous rate was substituted", exception.Message);
    }

    // TS03/E3: Sunat Provider Reports Network Failure Without Fallback; comprobación aislada.
    [Fact]
    [Trait("Story", "TS03"), Trait("Scenario", "E3"), Trait("Category", "Unit")]
    public async Task SunatProviderReportsNetworkFailureWithoutFallback()
    {
        var provider = CreateProvider(new FailingHandler());

        var exception = await Assert.ThrowsAsync<ExternalServiceUnavailableException>(
            () => provider.GetUsdToPenAsync(new DateOnly(2026, 9, 29)));

        Assert.Contains("could not be reached", exception.Message);
    }

    private static SunatExchangeRateProvider CreateProvider(HttpMessageHandler handler) =>
        new(
            new HttpClient(handler),
            Options.Create(new SunatExchangeRateOptions
            {
                Token = "test-token",
                Endpoint = "https://e-consulta.sunat.gob.pe/cl-at-ittipcam/tcS01Alias/listarTipoCambio"
            }),
            TimeProvider.System);

    // TS03/E3: respuestas vacías, mal formadas, solo compra y tasas no positivas.
    [Theory]
    [InlineData("[]")]
    [InlineData("not-json")]
    [InlineData("[{\"fecPublica\":\"29/09/2026\",\"valTipo\":\"3.4\",\"codTipo\":\"C\"}]")]
    [InlineData("[{\"fecPublica\":\"29/09/2026\",\"valTipo\":\"0\",\"codTipo\":\"V\"}]")]
    [InlineData("[{\"fecPublica\":\"29/09/2026\",\"valTipo\":\"-3.4\",\"codTipo\":\"V\"}]")]
    [Trait("Story", "TS03"), Trait("Scenario", "E3"), Trait("Category", "Unit")]
    public async Task InvalidOfficialResponsesNeverProduceAnExchangeRate(string response)
    {
        var provider = CreateProvider(new RecordingHandler(response));
        await Assert.ThrowsAsync<ExternalServiceUnavailableException>(() => provider.GetUsdToPenAsync(new DateOnly(2026, 9, 29)));
    }

    // TS03/E2: enero es mes cero y diciembre mes once, con año y fecha exactos.
    [Theory, InlineData(1, 0), InlineData(12, 11)]
    [Trait("Story", "TS03"), Trait("Scenario", "E2"), Trait("Category", "Unit")]
    public async Task SunatRequestUsesZeroBasedCalendarMonth(int month, int apiMonth)
    {
        var handler = new RecordingHandler($"[{{\"fecPublica\":\"01/{month:00}/2026\",\"valTipo\":\"3.5\",\"codTipo\":\"V\"}}]");
        var rate = await CreateProvider(handler).GetUsdToPenAsync(new DateOnly(2026, month, 1));
        Assert.Contains($"\"mes\":{apiMonth}", handler.Body);
        Assert.Contains("\"anio\":2026", handler.Body);
        Assert.Equal(new DateOnly(2026, month, 1), rate.PublishedOn);
        Assert.Equal("USD", rate.SourceCurrency); Assert.Equal("PEN", rate.TargetCurrency);
        Assert.InRange(rate.RetrievedAt, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow);
    }

    private sealed class RecordingHandler(string responseJson) : HttpMessageHandler
    {
        public string UserAgent { get; private set; } = string.Empty;
        public string Origin { get; private set; } = string.Empty;
        public string Body { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            UserAgent = request.Headers.UserAgent.ToString();
            Origin = request.Headers.GetValues("Origin").Single();
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class FailingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            throw new HttpRequestException("Simulated SUNAT outage");
    }
}
