namespace SmartQuote.Modules.IdentityAccess.Domain.Services;

public static class RegistrationPasswordPolicy
{
    public const int MinimumLength = 12;
    public const int MaximumLength = 128;

    public static void Validate(string password, string email)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinimumLength || password.Length > MaximumLength)
            throw new ArgumentException($"Password must contain between {MinimumLength} and {MaximumLength} characters.", nameof(password));

        if (password.Any(char.IsControl))
            throw new ArgumentException("Password must not contain control characters.", nameof(password));

        if (!password.Any(char.IsUpper) || !password.Any(char.IsLower) ||
            !password.Any(char.IsDigit) || !password.Any(character => char.IsPunctuation(character) || char.IsSymbol(character)))
        {
            throw new ArgumentException(
                "Password must include an uppercase letter, a lowercase letter, a number, and a symbol.",
                nameof(password));
        }

        var localPart = email.Trim().Split('@', 2)[0];
        if (localPart.Length >= 3 && password.Contains(localPart, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Password must not contain the email name.", nameof(password));
    }
}
