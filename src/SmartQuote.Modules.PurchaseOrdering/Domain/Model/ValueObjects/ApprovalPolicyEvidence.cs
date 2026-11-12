using SmartQuote.API.Shared.Domain;

namespace SmartQuote.API.PurchaseOrdering.Domain.Model.ValueObjects;

public sealed record ApprovalPolicyEvidence(Guid SupplierAccountId, long SupplierEligibilityVersion,
    long? BudgetVersion, decimal? BudgetAmountPen, decimal TotalPen, decimal? MarginPen,
    decimal? ExchangeRate, string? ExchangeRateSource, string? RateType, DateOnly? RatePublishedOn, DateTimeOffset? RateRetrievedAt);

public sealed record BudgetAssessment(string Status, long? BudgetVersion, decimal? BudgetAmountPen,
    decimal TotalPen, decimal? MarginPen, decimal? ExcessPen, bool CanApprove)
{
    // One decision may contain several supplier amounts: check their sum, never each order separately.
    public static BudgetAssessment Evaluate(long? version, decimal? limit, IReadOnlyList<decimal> decisionAmountsPen)
    {
        if (decisionAmountsPen.Count == 0 || decisionAmountsPen.Any(a => a < 0))
            throw new DomainException("The complete acquisition amount is required for budget control.");
        var total = decisionAmountsPen.Sum();
        if (!version.HasValue && !limit.HasValue) return new("NotConfigured", null, null, total, null, null, true);
        if (!version.HasValue || version <= 0 || !limit.HasValue || limit <= 0)
            throw new DomainException("The authorized budget is invalid.");
        var margin = limit.Value - total;
        return new(margin >= 0 ? "WithinBudget" : "Exceeded", version, limit, total, margin, Math.Max(0, -margin), margin >= 0);
    }
}
