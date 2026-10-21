namespace SmartQuote.Modules.QuotationIntake.Application.Ports;

public interface ISupplierRegistration
{
    Task<Guid> RegisterAsync(string taxIdentifier, string businessName, CancellationToken ct);
}
