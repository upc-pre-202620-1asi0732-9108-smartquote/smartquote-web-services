using System.Globalization;
using SmartQuote.API.Shared.Domain;
using SmartQuote.API.SupplyRequests.Domain.Model.Enums;

namespace SmartQuote.API.SupplyRequests.Domain.Model.ValueObjects;

public class TechnicalRequirement
{
    public TechnicalRequirementId Id { get; private set; } = null!;
    public string Name { get; private set; } = string.Empty;
    public ComparisonOperator Operator { get; private set; }
    public string ExpectedValue { get; private set; } = string.Empty;
    public string UnitOfMeasure { get; private set; } = string.Empty;
    public bool IsMandatory { get; private set; }

    private TechnicalRequirement() { }

    private TechnicalRequirement(
        TechnicalRequirementId id,
        string name,
        ComparisonOperator @operator,
        string expectedValue,
        string unitOfMeasure,
        bool isMandatory)
    {
        Id = id;
        Name = name.Trim();
        Operator = @operator;
        ExpectedValue = expectedValue.Trim();
        UnitOfMeasure = unitOfMeasure.Trim();
        IsMandatory = isMandatory;
    }

    public static TechnicalRequirement Create(
        string name,
        ComparisonOperator @operator,
        string expectedValue,
        string unitOfMeasure,
        bool isMandatory)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Technical requirement name is required.");

        if (!Enum.IsDefined(@operator))
            throw new DomainException("Technical requirement comparison operator is invalid.");

        if (string.IsNullOrWhiteSpace(expectedValue))
            throw new DomainException("Technical requirement expected value is required.");

        if (@operator is ComparisonOperator.GreaterThanOrEqual or ComparisonOperator.LessThanOrEqual)
        {
            if (!decimal.TryParse(expectedValue, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
                throw new DomainException("A numeric comparison requires a numeric expected value using invariant format.");

            if (string.IsNullOrWhiteSpace(unitOfMeasure))
                throw new DomainException("A numeric technical requirement requires a unit of measure.");
        }

        return new TechnicalRequirement(
            new TechnicalRequirementId(Guid.NewGuid()),
            name,
            @operator,
            expectedValue,
            unitOfMeasure ?? string.Empty,
            isMandatory);
    }
}
