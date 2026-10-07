using SmartQuote.API.Shared.Domain;
using SmartQuote.API.Shared.Domain.Model.ValueObjects;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.Entities;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.Enums;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.ValueObjects;

namespace SmartQuote.Modules.EvaluationSimulation.Domain.Model.Aggregates;

public class EvaluationScenario : AggregateRoot<EvaluationScenarioId>
{
    private const decimal RequiredWeightTotal = 100m;

    private readonly List<EvaluationCriterion> _criteria = [];

    public string RequestId { get; private set; } = string.Empty;
    public int Version { get; private set; }
    public ScenarioStatus Status { get; private set; }
    public UserId CreatedBy { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public EvaluationScenarioId? SupersedesScenarioId { get; private set; }

    public IReadOnlyList<EvaluationCriterion> Criteria => _criteria.AsReadOnly();

    private EvaluationScenario() { }

    public static EvaluationScenario Create(string requestId, UserId createdBy)
    {
        if (string.IsNullOrWhiteSpace(requestId))
            throw new DomainException("Request id is required to create an evaluation scenario.");

        return new EvaluationScenario
        {
            Id = new EvaluationScenarioId(Guid.NewGuid()),
            RequestId = requestId,
            Version = 1,
            Status = ScenarioStatus.Draft,
            CreatedBy = createdBy,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public void AddCriterion(EvaluationCriterion criterion)
    {
        if (Status != ScenarioStatus.Draft)
            throw new DomainException($"Cannot add criteria to a scenario in status '{Status}'.");

        if (_criteria.Any(current => current.DisplayOrder == criterion.DisplayOrder))
            throw new DomainException($"Criterion display order '{criterion.DisplayOrder}' is duplicated.");

        if (_criteria.Any(current =>
                current.TargetField.Equals(criterion.TargetField, StringComparison.OrdinalIgnoreCase) &&
                current.Mode == criterion.Mode))
            throw new DomainException($"Criterion target '{criterion.TargetField}' is duplicated for mode '{criterion.Mode}'.");

        _criteria.Add(criterion);
    }

    public void ValidateWeights()
    {
        if (_criteria.Count == 0)
            throw new DomainException("An evaluation scenario must have at least one criterion.");

        if (_criteria.All(criterion => criterion.Mode != CriterionMode.Mandatory))
            throw new DomainException("An evaluation scenario must contain at least one mandatory criterion.");

        if (_criteria.All(criterion => criterion.Mode != CriterionMode.Weighted))
            throw new DomainException("An evaluation scenario must contain at least one weighted criterion.");

        var weightedTotal = _criteria.Where(c => c.Mode == CriterionMode.Weighted).Sum(c => c.Weight);

        if (_criteria.Any(c => c.Mode == CriterionMode.Weighted) && weightedTotal != RequiredWeightTotal)
            throw new DomainException($"Weighted criteria must add up to {RequiredWeightTotal}, but they add up to {weightedTotal}.");
    }

    public void Activate()
    {
        ValidateWeights();
        Status = ScenarioStatus.Active;
    }

    public void MarkSuperseded() => Status = ScenarioStatus.Superseded;

    public EvaluationScenario CreateNextVersion(UserId createdBy)
    {
        var next = new EvaluationScenario
        {
            Id = new EvaluationScenarioId(Guid.NewGuid()),
            RequestId = RequestId,
            Version = Version + 1,
            Status = ScenarioStatus.Draft,
            CreatedBy = createdBy,
            CreatedAt = DateTimeOffset.UtcNow,
            SupersedesScenarioId = Id
        };

        return next;
    }

    public InputFingerprint CalculateDefinitionFingerprint() =>
        InputFingerprint.FromParts(
            Id.ToString(),
            RequestId,
            Version.ToString(),
            string.Join('|', _criteria
                .OrderBy(criterion => criterion.DisplayOrder)
                .Select(criterion => string.Join(':',
                    criterion.Id,
                    criterion.Name,
                    criterion.TargetField,
                    criterion.Category,
                    criterion.Mode,
                    criterion.Operator,
                    criterion.ExpectedValue,
                    criterion.UnitOfMeasure,
                    criterion.Weight,
                    criterion.DisplayOrder))));
}
