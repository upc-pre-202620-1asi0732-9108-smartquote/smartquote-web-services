using SmartQuote.API.Shared.Domain;
using SmartQuote.Modules.EvaluationSimulation.Application.Ports;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.Aggregates;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.Enums;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.ValueObjects;

namespace SmartQuote.Modules.EvaluationSimulation.Application;

public record PreparedEvaluation(EvaluationScenario Scenario, EvaluationDataset Dataset, ExchangeRateSnapshot? Rate, DateOnly Today);
public sealed class EvaluationPreparationService(IEvaluationScenarioRepository scenarios, IPurchaseRequestSnapshotReader requests,
    IVerifiedQuotationSnapshotReader quotes, ISupplierEligibilityReader suppliers, EvaluationInputAssembler assembler,
    IExchangeRateProvider rates, TimeProvider clock)
{
    public async Task<PreparedEvaluation> PrepareAsync(Guid scenarioId, bool preserveRate, ExchangeRateSnapshot? storedRate, CancellationToken ct)
    {
        var scenario = await scenarios.GetByIdAsync(new(scenarioId), ct) ?? throw new NotFoundException("Evaluation scenario was not found.");
        if (scenario.Status != ScenarioStatus.Active) throw new ConflictException("The scenario is no longer active.");
        var request = await requests.GetCurrentAsync(scenario.RequestId, ct) ?? throw new NotFoundException("Purchase request was not found.");
        if (request.Status != "Evaluation") throw new DomainException("The purchase request must be in Evaluation status.");
        if (request.TechnicalRevision != scenario.TechnicalRevision) throw new ConflictException("The technical specification changed; update the scenario.");
        var snapshots = await quotes.GetCurrentForRequestAsync(scenario.RequestId, ct);
        if (snapshots.Count < 2) throw new DomainException("At least two verified quotations are required.");
        if (snapshots.Any(q => q.Currency is not ("PEN" or "USD"))) throw new DomainException("Only PEN and USD quotations can be evaluated.");
        var eligibility = await suppliers.ReadAsync(snapshots.Select(q => q.SupplierTaxIdentifier.Trim()).Distinct().ToList(), ct);
        var dataset = assembler.Assemble(request, snapshots, eligibility);
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime.AddHours(-5));
        var rate = preserveRate ? storedRate : snapshots.Any(q => q.Currency == "USD") ? await rates.GetUsdToPenAsync(today, ct) : null;
        if (snapshots.Any(q => q.Currency == "USD") && rate is null) throw new DomainException("The preserved USD conversion evidence is missing.");
        return new(scenario, dataset, rate, today);
    }
}
