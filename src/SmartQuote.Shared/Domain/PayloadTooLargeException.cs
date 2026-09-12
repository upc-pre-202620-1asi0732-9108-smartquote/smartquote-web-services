namespace SmartQuote.API.Shared.Domain;

public class PayloadTooLargeException(string message) : Exception(message);
