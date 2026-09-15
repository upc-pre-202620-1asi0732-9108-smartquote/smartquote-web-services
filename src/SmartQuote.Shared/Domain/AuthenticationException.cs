namespace SmartQuote.API.Shared.Domain;

public sealed class AuthenticationException(string message) : Exception(message);
