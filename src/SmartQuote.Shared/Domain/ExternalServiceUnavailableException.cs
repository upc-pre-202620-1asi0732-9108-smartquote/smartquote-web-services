namespace SmartQuote.API.Shared.Domain;

public class ExternalServiceUnavailableException(string message, Exception? innerException = null) : Exception(message, innerException);
