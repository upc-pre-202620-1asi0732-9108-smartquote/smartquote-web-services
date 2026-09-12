namespace SmartQuote.API.Shared.Application.Security;

public static class SmartQuoteRoles
{
    public const string ProductionSpecialist = nameof(ProductionSpecialist);
    public const string PurchaseAnalyst = nameof(PurchaseAnalyst);
    public const string PurchaseManager = nameof(PurchaseManager);

    public const string PurchasingStaff = PurchaseAnalyst + "," + PurchaseManager;
}
