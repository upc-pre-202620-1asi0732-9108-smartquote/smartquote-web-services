using SmartQuote.API.SupplyRequests.Domain.Model.ValueObjects;

namespace SmartQuote.API.SupplyRequests.Domain.Model.Entities;

public sealed record RequestAssignmentEntry(Guid Id, Guid? PreviousAnalystId, Guid AnalystId,
    Guid AssignedBy, DateTimeOffset AssignedAt, string? Reason);

public sealed record RequirementDefinition(string Name, string Operator, string ExpectedValue,
    string UnitOfMeasure, bool IsMandatory)
{
    public static RequirementDefinition From(TechnicalRequirement requirement) => new(requirement.Name,
        requirement.Operator.ToString(), requirement.ExpectedValue, requirement.UnitOfMeasure, requirement.IsMandatory);
}

public sealed record RequirementRevision(Guid Id, Guid ClarificationId, Guid ItemId, Guid RequirementId,
    long TechnicalRevision, RequirementDefinition Previous, RequirementDefinition Current,
    Guid ChangedBy, DateTimeOffset ChangedAt, string Reason)
{
    private RequirementRevision() : this(Guid.Empty, Guid.Empty, Guid.Empty, Guid.Empty, 0, null!, null!, Guid.Empty, default, "") { }
}
