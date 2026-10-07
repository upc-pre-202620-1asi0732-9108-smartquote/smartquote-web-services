namespace SmartQuote.API.SupplyRequests.Domain.Model.ValueObjects;

public record RequestAttachmentId
{
    public Guid Value { get; }

    public RequestAttachmentId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("RequestAttachmentId cannot be empty.", nameof(value));

        Value = value;
    }

    public static implicit operator Guid(RequestAttachmentId id) => id.Value;

    public override string ToString() => Value.ToString();
}
