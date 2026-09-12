namespace SmartQuote.API.Shared.Domain;

public class ConflictException(string message) : Exception(message);
