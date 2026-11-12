namespace SmartQuote.API.Shared.Domain;

public class ConflictException(string message, string code = "state_conflict", IReadOnlyDictionary<string, object?>? details = null) : Exception(message)
{
    public string Code { get; } = code;
    public IReadOnlyDictionary<string, object?>? Details { get; } = details;
}
