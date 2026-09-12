using SmartQuote.API.Shared.Domain;

namespace SmartQuote.API.SupplyRequests.Interfaces.REST.Transform;

internal static class EnumResourceParser
{
    public static TEnum Parse<TEnum>(string value, string fieldName) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value) ||
            int.TryParse(value, out _) ||
            !Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) ||
            !Enum.IsDefined(parsed))
            throw new DomainException($"'{value}' is not a valid {fieldName}.");

        return parsed;
    }
}
