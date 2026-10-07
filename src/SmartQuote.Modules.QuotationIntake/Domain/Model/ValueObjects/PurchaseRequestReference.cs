using SmartQuote.API.Shared.Domain;

namespace SmartQuote.Modules.QuotationIntake.Domain.Model.ValueObjects;

public record PurchaseRequestReference
{
    public string RequestId { get; }

    public PurchaseRequestReference(string requestId)
    {
        if (string.IsNullOrWhiteSpace(requestId))
            throw new DomainException("Purchase request reference id is required.");

        RequestId = requestId;
    }
}
