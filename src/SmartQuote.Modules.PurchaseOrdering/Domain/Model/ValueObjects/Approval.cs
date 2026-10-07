using SmartQuote.API.Shared.Domain;
using SmartQuote.API.Shared.Domain.Model.ValueObjects;

namespace SmartQuote.API.PurchaseOrdering.Domain.Model.ValueObjects;

public record Approval
{
    public UserId ApprovedBy { get; }
    public DateTimeOffset ApprovedAt { get; }
    public string IdempotencyKey { get; }

    public Approval(UserId approvedBy, DateTimeOffset approvedAt, string idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new DomainException("Approval idempotency key is required.");

        ApprovedBy = approvedBy;
        ApprovedAt = approvedAt;
        IdempotencyKey = idempotencyKey;
    }
}
