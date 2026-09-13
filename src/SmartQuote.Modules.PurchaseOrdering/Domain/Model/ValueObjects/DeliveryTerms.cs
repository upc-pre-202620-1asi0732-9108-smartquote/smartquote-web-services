using SmartQuote.API.Shared.Domain;

namespace SmartQuote.API.PurchaseOrdering.Domain.Model.ValueObjects;

public sealed record DeliveryTerms
{
    public int LeadTimeDays { get; private set; }
    public string Conditions { get; private set; } = string.Empty;
    public string Destination { get; private set; } = string.Empty;

    private DeliveryTerms() { }

    public static DeliveryTerms Create(int leadTimeDays, string conditions, string destination)
    {
        if (leadTimeDays < 0)
            throw new DomainException("Delivery lead time cannot be negative.");
        if (string.IsNullOrWhiteSpace(conditions))
            throw new DomainException("Delivery conditions are required.");
        if (string.IsNullOrWhiteSpace(destination))
            throw new DomainException("Delivery destination is required.");

        return new DeliveryTerms
        {
            LeadTimeDays = leadTimeDays,
            Conditions = conditions.Trim(),
            Destination = destination.Trim()
        };
    }
}
