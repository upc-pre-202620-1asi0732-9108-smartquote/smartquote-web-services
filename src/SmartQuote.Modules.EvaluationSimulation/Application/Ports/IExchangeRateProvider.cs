using SmartQuote.Modules.EvaluationSimulation.Domain.Model.ValueObjects;

namespace SmartQuote.Modules.EvaluationSimulation.Application.Ports;

public interface IExchangeRateProvider
{
    Task<ExchangeRateSnapshot> GetUsdToPenAsync(DateOnly applicableDate, CancellationToken cancellationToken = default);
}
