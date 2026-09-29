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
    [Fact]
    public void SnapshotConvertsUsdAndPreservesPen()
    {
        var snapshot = new ExchangeRateSnapshot(
            "USD", "PEN", 3.44m, "V", new DateOnly(2026, 9, 29),
            "SUNAT - Consulta de Tipo de Cambio", DateTimeOffset.UtcNow);

        Assert.Equal(new Money(344m, "PEN"), snapshot.Convert(new Money(100m, "USD")));
        Assert.Equal(new Money(100m, "PEN"), snapshot.Convert(new Money(100m, "PEN")));
    }

    [Fact]
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

    [Fact]
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

    [Fact]
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
