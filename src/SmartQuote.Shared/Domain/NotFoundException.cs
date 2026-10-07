namespace SmartQuote.API.Shared.Domain;

public class NotFoundException(string message) : Exception(message);
