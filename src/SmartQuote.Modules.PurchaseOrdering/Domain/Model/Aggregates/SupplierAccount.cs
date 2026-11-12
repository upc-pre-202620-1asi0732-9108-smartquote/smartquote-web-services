using System.Text.RegularExpressions;
using SmartQuote.API.Shared.Domain;

namespace SmartQuote.API.PurchaseOrdering.Domain.Model.Aggregates;

public sealed record SupplierEligibilityEntry(Guid Id, string PreviousStatus, string NewStatus,
    Guid DecidedBy, DateTimeOffset DecidedAt, string Reason, long Version);

public sealed class SupplierAccount
{
    private readonly List<SupplierEligibilityEntry> _history = [];
    public Guid Id { get; private set; }
    public string TaxIdentifier { get; private set; } = "";
    public string BusinessName { get; private set; } = "";
    public string Status { get; private set; } = "Pending";
    public long Version { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public IReadOnlyList<SupplierEligibilityEntry> History => _history.AsReadOnly();
    private SupplierAccount() { }

    public static string NormalizeTaxIdentifier(string value)
    {
        var normalized = value?.Trim() ?? "";
        if (!Regex.IsMatch(normalized, "^[0-9]{11}$", RegexOptions.CultureInvariant))
            throw new DomainException("An identified supplier requires an eleven-digit tax identifier (RUC).");
        return normalized;
    }
    public static SupplierAccount Register(string taxIdentifier, string businessName, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(businessName) || businessName.Trim().Length > 200)
            throw new DomainException("Supplier business name must contain 1–200 characters.");
        return new() { Id = Guid.NewGuid(), TaxIdentifier = NormalizeTaxIdentifier(taxIdentifier),
            BusinessName = businessName.Trim(), CreatedAt = now, Version = 1 };
    }
    public void Decide(string action, string reason, Guid actorId, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 2000)
            throw new DomainException("A decision reason of 1–2000 characters is required.");
        var next = (action, Status) switch
        {
            ("enable", "Pending") => "Enabled",
            ("suspend", "Enabled") => "Suspended",
            ("reactivate", "Suspended") => "Enabled",
            _ => throw new DomainException("This supplier transition is not allowed.")
        };
        var previous = Status;
        Status = next;
        Version++;
        _history.Add(new(Guid.NewGuid(), previous, next, actorId, now, reason.Trim(), Version));
    }
}
