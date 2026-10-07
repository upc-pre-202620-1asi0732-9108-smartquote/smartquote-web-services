namespace SmartQuote.Modules.QuotationIntake.Interfaces.REST.Resource;

public sealed record ConfirmQuotationResource(
    IReadOnlyList<QuotationLineMappingResource> LineMappings,
    long ExpectedVersion);

public sealed record QuotationLineMappingResource(Guid LineId, Guid RequestedItemId);
