using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using SmartQuote.API.Shared.Domain;
using SmartQuote.Modules.EvaluationSimulation.Application.Ports;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.ValueObjects;

namespace SmartQuote.Modules.EvaluationSimulation.Infrastructure.ExchangeRates;

public sealed class SunatExchangeRateProvider(
    HttpClient httpClient,
    IOptions<SunatExchangeRateOptions> options,
    TimeProvider timeProvider) : IExchangeRateProvider
{
    private const string OfficialSource = "SUNAT - Consulta de Tipo de Cambio";
    private readonly SunatExchangeRateOptions _options = options.Value;

    public async Task<ExchangeRateSnapshot> GetUsdToPenAsync(
        DateOnly applicableDate,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Token))
            throw new ExternalServiceUnavailableException(
                "USD conversion could not be completed because the SUNAT exchange-rate token is not configured.");

        // The SUNAT endpoint uses a zero-based month (January = 0), as its web client does.
        var payload = new SunatRequest(applicableDate.Year, applicableDate.Month - 1, _options.Token);
        using var request = new HttpRequestMessage(HttpMethod.Post, _options.Endpoint)
        {
            Content = JsonContent.Create(payload)
        };

        AddBrowserIdentificationHeaders(request);

        try
        {
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new ExternalServiceUnavailableException(
                    $"SUNAT did not provide an exchange rate (HTTP {(int)response.StatusCode}). No previous rate was substituted.");

            var entries = await response.Content.ReadFromJsonAsync<List<SunatResponse>>(cancellationToken: cancellationToken)
                          ?? [];

            var applicable = entries.SingleOrDefault(entry =>
                entry.Type.Equals("V", StringComparison.OrdinalIgnoreCase) &&
                DateOnly.TryParseExact(entry.PublishedDate, "dd/MM/yyyy", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var publishedOn) &&
                publishedOn == applicableDate);

            if (applicable is null ||
                !decimal.TryParse(applicable.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var rate) ||
                rate <= 0)
            {
                throw new ExternalServiceUnavailableException(
                    $"SUNAT did not publish a valid USD/PEN sale rate for {applicableDate:yyyy-MM-dd}. No previous rate was substituted.");
            }

            return new ExchangeRateSnapshot(
                "USD",
                "PEN",
                rate,
                "V",
                applicableDate,
                OfficialSource,
                timeProvider.GetUtcNow());
        }
        catch (ExternalServiceUnavailableException)
        {
            throw;
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ExternalServiceUnavailableException(
                "SUNAT did not respond before the exchange-rate request timed out. No previous rate was substituted.", exception);
        }
        catch (HttpRequestException exception)
        {
            throw new ExternalServiceUnavailableException(
                "SUNAT could not be reached and the USD conversion was not completed. No previous rate was substituted.", exception);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new ExternalServiceUnavailableException(
                "SUNAT returned an invalid exchange-rate response. No previous rate was substituted.", exception);
        }
    }

    private static void AddBrowserIdentificationHeaders(HttpRequestMessage request)
    {
        request.Headers.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/140.0.0.0 Safari/537.36");
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.AcceptLanguage.ParseAdd("es-PE,es;q=0.9");
        request.Headers.Referrer = new Uri("https://e-consulta.sunat.gob.pe/cl-at-ittipcam/tcS01Alias");
        request.Headers.TryAddWithoutValidation("Origin", "https://e-consulta.sunat.gob.pe");
    }

    private sealed record SunatRequest(
        [property: JsonPropertyName("anio")] int Year,
        [property: JsonPropertyName("mes")] int Month,
        [property: JsonPropertyName("token")] string Token);

    private sealed record SunatResponse(
        [property: JsonPropertyName("fecPublica")] string PublishedDate,
        [property: JsonPropertyName("valTipo")] string Value,
        [property: JsonPropertyName("codTipo")] string Type);
}
