using SmartQuote.API.Shared.Domain;

namespace SmartQuote.Modules.QuotationIntake.Domain.Model.Aggregates;

/// <summary>US17: durable preventive alert. Reading it never extends the offer's validity.</summary>
public sealed class QuotationExpiryAlert
{
    public Guid Id { get; private set; }
    public Guid QuotationId { get; private set; }
    public Guid RequestId { get; private set; }
    public Guid RecipientId { get; private set; }
    public string SupplierName { get; private set; } = "";
    public DateOnly ValidUntil { get; private set; }
    public int LeadDays { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ReadAt { get; private set; }
    public DateTimeOffset? ReassignedAt { get; private set; }
    private QuotationExpiryAlert() { }
    public static QuotationExpiryAlert Create(Guid quotation, Guid request, Guid recipient, string supplier,
        DateOnly expiry, int leadDays, DateOnly today, DateTimeOffset now)
    {
        if (recipient == Guid.Empty || leadDays < 1 || expiry < today || expiry > today.AddDays(leadDays))
            throw new DomainException("A preventive alert requires an assigned analyst and a current offer inside the configured window.");
        return new() { Id = Guid.NewGuid(), QuotationId = quotation, RequestId = request, RecipientId = recipient,
            SupplierName = supplier, ValidUntil = expiry, LeadDays = leadDays, CreatedAt = now };
    }
    public void Transfer(Guid recipient, DateTimeOffset now)
    {
        if (recipient == Guid.Empty) throw new DomainException("An alert recipient is required.");
        if (RecipientId == recipient) return;
        RecipientId = recipient; ReassignedAt = now;
    }
    public void Read(Guid actor, DateTimeOffset now)
    {
        if (actor != RecipientId) throw new UnauthorizedAccessException("This alert belongs to another analyst.");
        ReadAt ??= now;
    }
}
