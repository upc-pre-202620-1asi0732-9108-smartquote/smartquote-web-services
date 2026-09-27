namespace SmartQuote.Modules.QuotationIntake.Interfaces.REST.Resource;

public sealed record AddSpecificationResource(
    string Name,
    string Value,
    string UnitOfMeasure,
    int SourcePageNumber,
    string SourceTextReference,
    string Reason,
    long ExpectedVersion);
