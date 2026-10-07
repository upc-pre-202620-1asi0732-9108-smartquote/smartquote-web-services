namespace SmartQuote.API.Shared.Domain;

public class UnprocessableDocumentException(string message, Exception? innerException = null) : Exception(message, innerException);
