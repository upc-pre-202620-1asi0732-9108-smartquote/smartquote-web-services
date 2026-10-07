using SmartQuote.API.Shared.Domain;
using SmartQuote.API.SupplyRequests.Domain.Model.ValueObjects;

namespace SmartQuote.API.SupplyRequests.Domain.Model.Entities;

public class RequestedItem
{
    private readonly List<TechnicalRequirement> _requirements = [];

    public RequestedItemId Id { get; private set; } = null!;
    public int LineNumber { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public decimal Quantity { get; private set; }
    public string UnitOfMeasure { get; private set; } = string.Empty;

    public IReadOnlyList<TechnicalRequirement> Requirements => _requirements.AsReadOnly();

    private RequestedItem() { }

    private RequestedItem(RequestedItemId id, string description, decimal quantity, string unitOfMeasure)
    {
        Id = id;
        Description = description;
        Quantity = quantity;
        UnitOfMeasure = unitOfMeasure;
    }

    public static RequestedItem Create(string description, decimal quantity, string unitOfMeasure)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new DomainException("Requested item description is required.");

        if (quantity <= 0)
            throw new DomainException("Requested item quantity must be greater than zero.");

        if (string.IsNullOrWhiteSpace(unitOfMeasure))
            throw new DomainException("Requested item unit of measure is required.");

        return new RequestedItem(new RequestedItemId(Guid.NewGuid()), description, quantity, unitOfMeasure);
    }

    public void AddRequirement(TechnicalRequirement requirement)
    {
        if (_requirements.Any(current => current.Name.Equals(requirement.Name, StringComparison.OrdinalIgnoreCase)))
            throw new DomainException($"Technical requirement '{requirement.Name}' is duplicated for the requested item.");

        _requirements.Add(requirement);
    }

    public bool HasMandatoryRequirement() => _requirements.Any(requirement => requirement.IsMandatory);

    internal void AssignLineNumber(int lineNumber) => LineNumber = lineNumber;
}
