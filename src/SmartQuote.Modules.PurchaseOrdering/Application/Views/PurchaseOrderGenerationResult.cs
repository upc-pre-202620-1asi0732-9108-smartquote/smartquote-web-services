namespace SmartQuote.API.PurchaseOrdering.Application.Views;

public sealed record PurchaseOrderGenerationResult(PurchaseOrderView Order, bool WasCreated);
