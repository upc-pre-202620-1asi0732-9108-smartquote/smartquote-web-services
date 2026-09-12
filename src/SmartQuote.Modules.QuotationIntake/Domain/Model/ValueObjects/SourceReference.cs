using SmartQuote.API.Shared.Domain;

namespace SmartQuote.Modules.QuotationIntake;

public record SourceReference
{
    public int PageNumber { get; }
    public string TextReference { get; }

    public SourceReference(int pageNumber, string textReference)
    {
        if (pageNumber <= 0)
            throw new DomainException("Source page number must be greater than zero.");

        PageNumber = pageNumber;
        TextReference = textReference ?? string.Empty;
    }
}
