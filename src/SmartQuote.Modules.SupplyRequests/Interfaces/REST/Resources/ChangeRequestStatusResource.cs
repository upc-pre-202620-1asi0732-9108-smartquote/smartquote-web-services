namespace SmartQuote.API.SupplyRequests.Interfaces.REST.Resources;

public record ChangeRequestStatusResource(string NextStatus, string Reason, long ExpectedVersion);
