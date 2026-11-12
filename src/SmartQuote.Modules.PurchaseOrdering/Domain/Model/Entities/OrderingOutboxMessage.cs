namespace SmartQuote.API.PurchaseOrdering.Domain.Model.Entities;

public sealed class OrderingOutboxMessage
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = "";
    public string Payload { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
}
