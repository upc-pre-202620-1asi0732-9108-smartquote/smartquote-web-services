namespace SmartQuote.API.SupplyRequests.Domain.Model.Enums;

public enum RequestStatus
{
    Draft,
    Submitted,
    UnderReview,
    QuotationCollection,
    Evaluation,
    Approved,
    Ordered,
    Rejected,
    Cancelled
}
