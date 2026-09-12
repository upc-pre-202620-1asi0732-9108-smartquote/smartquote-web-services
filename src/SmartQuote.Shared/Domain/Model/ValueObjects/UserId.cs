namespace SmartQuote.API.Shared.Domain.Model.ValueObjects;

public record UserId
{
    public Guid Value { get; }

    public UserId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty.", nameof(value));

        Value = value;
    }

    public static implicit operator Guid(UserId userId) => userId.Value;

    public override string ToString() => Value.ToString();
}
