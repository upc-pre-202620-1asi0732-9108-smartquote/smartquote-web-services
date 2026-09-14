using SmartQuote.API.Shared.Domain;
using SmartQuote.API.Shared.Application.Security;
using SmartQuote.API.Shared.Domain.Model.ValueObjects;
using SmartQuote.API.SupplyRequests.Application.OutboundServices;
using SmartQuote.Modules.EvaluationSimulation.Application.Ports;
using SmartQuote.Modules.EvaluationSimulation.Application.Views;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.Aggregates;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.Commands;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.Entities;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.ValueObjects;

namespace SmartQuote.Modules.EvaluationSimulation.Application;

public class ScenarioApplicationService(
    IEvaluationScenarioRepository repository,
    IEvaluationSimulationUnitOfWork unitOfWork,
    IPurchaseRequestSnapshotReader requestReader,
    ICurrentUser currentUser)
{
    public async Task<EvaluationScenarioId> CreateAsync(CreateEvaluationScenarioCommand command, CancellationToken cancellationToken = default)
    {
        var request = await GetEligibleRequestAsync(command.RequestId, cancellationToken);
        if (await repository.GetCurrentForRequestAsync(command.RequestId, cancellationToken) is not null)
            throw new ConflictException($"Purchase request '{command.RequestId}' already has an evaluation scenario.");

        ValidateTechnicalTargets(command.Criteria, request);
        var scenario = EvaluationScenario.Create(command.RequestId, new UserId(currentUser.UserId));

        foreach (var criterionData in command.Criteria)
            scenario.AddCriterion(BuildCriterion(criterionData));

        scenario.Activate();

        await repository.AddAsync(scenario, cancellationToken);
        await unitOfWork.CompleteAsync(cancellationToken);

        return scenario.Id;
    }

    public async Task<EvaluationScenarioId> CreateNextVersionAsync(CreateNextScenarioVersionCommand command, CancellationToken cancellationToken = default)
    {
        var current = await repository.GetByIdAsync(new EvaluationScenarioId(command.ScenarioId), cancellationToken)
            ?? throw new NotFoundException($"Evaluation scenario '{command.ScenarioId}' was not found.");

        var currentForRequest = await repository.GetCurrentForRequestAsync(current.RequestId, cancellationToken);
        if (currentForRequest?.Id != current.Id || current.Status != Domain.Model.Enums.ScenarioStatus.Active)
            throw new ConflictException("Only the current active evaluation scenario can be versioned.");

        var request = await GetEligibleRequestAsync(current.RequestId, cancellationToken);
        ValidateTechnicalTargets(command.Criteria, request);
        var next = current.CreateNextVersion(new UserId(currentUser.UserId));

        foreach (var criterionData in command.Criteria)
            next.AddCriterion(BuildCriterion(criterionData));

        next.Activate();
        current.MarkSuperseded();

        await repository.AddAsync(next, cancellationToken);
        await unitOfWork.CompleteAsync(cancellationToken);

        return next.Id;
    }

    public async Task<EvaluationScenarioView> GetCurrentForRequestAsync(string requestId, CancellationToken cancellationToken = default)
    {
        var scenario = await repository.GetCurrentForRequestAsync(requestId, cancellationToken)
            ?? throw new NotFoundException($"No evaluation scenario was found for purchase request '{requestId}'.");

        return ToView(scenario);
    }

    public async Task<EvaluationScenarioView> GetByIdAsync(Guid scenarioId, CancellationToken cancellationToken = default)
    {
        var scenario = await repository.GetByIdAsync(new EvaluationScenarioId(scenarioId), cancellationToken)
            ?? throw new NotFoundException($"Evaluation scenario '{scenarioId}' was not found.");
        return ToView(scenario);
    }

    private async Task<PurchaseRequestSnapshot> GetEligibleRequestAsync(string requestId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(requestId, out _))
            throw new ArgumentException("Purchase request id must be a valid UUID.");

        var request = await requestReader.GetCurrentAsync(requestId, cancellationToken)
            ?? throw new NotFoundException($"Purchase request '{requestId}' was not found.");

        if (request.Status is not ("QuotationCollection" or "Evaluation"))
            throw new DomainException("Evaluation scenarios can only be configured while quotations are being collected or evaluated.");

        return request;
    }

    private static void ValidateTechnicalTargets(IReadOnlyList<CriterionData> criteria, PurchaseRequestSnapshot request)
    {
        var requirementIds = request.Items
            .SelectMany(item => item.Requirements)
            .Select(requirement => requirement.RequirementId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var unknownTarget = criteria
            .Where(criterion => criterion.Category == Domain.Model.Enums.CriterionCategory.TechnicalCompliance)
            .Select(criterion => criterion.TargetField)
            .FirstOrDefault(target => !requirementIds.Contains(target));

        if (unknownTarget is not null)
            throw new DomainException($"Technical criterion target '{unknownTarget}' is not part of the purchase request.");
    }

    private static EvaluationCriterion BuildCriterion(CriterionData data) =>
        EvaluationCriterion.Create(data.Name, data.TargetField, data.Category, data.Mode, data.Operator, data.ExpectedValue, data.UnitOfMeasure, data.Weight, data.DisplayOrder);

    private static EvaluationScenarioView ToView(EvaluationScenario scenario) => new(
        scenario.Id,
        scenario.RequestId,
        scenario.Version,
        scenario.Status.ToString(),
        scenario.CreatedBy,
        scenario.CreatedAt,
        scenario.SupersedesScenarioId is null ? null : (Guid)scenario.SupersedesScenarioId,
        scenario.Criteria.Select(criterion => new EvaluationCriterionView(
            criterion.Id,
            criterion.Name,
            criterion.TargetField,
            criterion.Category.ToString(),
            criterion.Mode.ToString(),
            criterion.Operator.ToString(),
            criterion.ExpectedValue,
            criterion.UnitOfMeasure,
            criterion.Weight,
            criterion.DisplayOrder)).ToList());
}
