namespace SmartQuote.Modules.EvaluationSimulation.Infrastructure.ExchangeRates;

public sealed class SunatExchangeRateOptions
{
    public const string SectionName = "SunatExchangeRate";

    public string Endpoint { get; set; } =
        "https://e-consulta.sunat.gob.pe/cl-at-ittipcam/tcS01Alias/listarTipoCambio";

    public string Token { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 15;
}
