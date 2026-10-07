namespace SmartQuote.API.SupplyRequests.Domain.Model.ValueObjects;

public sealed record TechnicalRequirementId(Guid Value)
{
    public static implicit operator Guid(TechnicalRequirementId id) => id.Value;
    public override string ToString() => Value.ToString();
}
