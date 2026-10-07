using System.Security.Cryptography;
using System.Text;

namespace SmartQuote.Modules.EvaluationSimulation.Domain.Model.ValueObjects;

public record InputFingerprint
{
    public string Value { get; }

    public InputFingerprint(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 64)
            throw new ArgumentException("InputFingerprint must be a 64-character SHA-256 hex hash.", nameof(value));

        Value = value;
    }

    public bool Matches(InputFingerprint other) => Value == other.Value;

    public static InputFingerprint FromParts(params string[] parts)
    {
        var joined = string.Join('|', parts);
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(joined)));
        return new InputFingerprint(hash);
    }
}
