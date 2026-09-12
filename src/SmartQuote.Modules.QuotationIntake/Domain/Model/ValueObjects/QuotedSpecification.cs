using SmartQuote.API.Shared.Domain;

namespace SmartQuote.Modules.QuotationIntake.Domain.Model.ValueObjects;

public record QuotedSpecification
{
    public string Name { get; }
    public string Value { get; }
    public string UnitOfMeasure { get; }

    public QuotedSpecification(string name, string value, string unitOfMeasure)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Specification name is required.");

        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("Specification value is required.");

        Name = name;
        Value = value;
        UnitOfMeasure = unitOfMeasure ?? string.Empty;
    }
}
