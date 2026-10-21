using SmartQuote.API.PurchaseOrdering.Application.Ports;
using SmartQuote.API.SupplyRequests.Application.OutboundServices;
using SmartQuote.Modules.EvaluationSimulation.Application.Ports;
using SmartQuote.Modules.PurchaseOrdering.Application.OutboundServices;
using SmartQuote.Modules.QuotationIntake.Application.Ports;

namespace SmartQuote.API.Integration;

public sealed class SupplierRegistrationAdapter(ISupplierDirectory directory) : ISupplierRegistration
{
    public Task<Guid> RegisterAsync(string taxIdentifier, string businessName, CancellationToken ct) => directory.RegisterIdentifiedAsync(taxIdentifier, businessName, ct);
}
public sealed class SupplierEligibilityAdapter(ISupplierDirectory directory) : ISupplierEligibilityReader
{
    public async Task<IReadOnlyList<SupplierEligibilityData>> ReadAsync(IReadOnlyList<string> taxIdentifiers, CancellationToken ct) =>
        (await directory.GetEligibilityAsync(taxIdentifiers, ct)).Select(s => new SupplierEligibilityData(s.SupplierId, s.TaxIdentifier, s.Status, s.Version)).ToList();
}
public sealed class OrderingRequestReader(IPurchaseRequestSnapshotProvider requests) : IOrderingRequestReader
{
    public async Task<bool> IsActiveAsync(Guid requestId, CancellationToken ct)
    {
        var request = await requests.GetSnapshotAsync(requestId, ct) ?? throw new SmartQuote.API.Shared.Domain.NotFoundException("Purchase request was not found.");
        if (request.Status == "Draft") throw new UnauthorizedAccessException("Draft requests are private to their specialist.");
        return request.Status is "Submitted" or "UnderReview" or "QuotationCollection" or "Evaluation" or "Approved";
    }
}
