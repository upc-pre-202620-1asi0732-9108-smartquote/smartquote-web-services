using System.Globalization;
using System.Text.RegularExpressions;
using SmartQuote.API.Shared.Domain;
using SmartQuote.API.Shared.Domain.Model.ValueObjects;
using SmartQuote.Modules.QuotationIntake.Domain.Model.Entities;
using SmartQuote.Modules.QuotationIntake.Domain.Model.Enums;
using SmartQuote.Modules.QuotationIntake.Domain.Model.Events;
using SmartQuote.Modules.QuotationIntake.Domain.Model.ValueObjects;

namespace SmartQuote.Modules.QuotationIntake.Domain.Model.Aggregates;

public class PoultryQuote : AggregateRoot<PoultryQuoteId>
{
    private static readonly Regex LineFieldPath = new(
        "^lines\\[(?<index>\\d+)\\]\\.(?<field>description|quantity|unitOfMeasure|unitPrice)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly List<QuotationLine> _lines = [];
    private readonly List<ExtractedField> _fields = [];

    public PurchaseRequestReference RequestReference { get; private set; } = null!;
    public SupplierReference Supplier { get; private set; } = null!;
    public SourceDocument SourceDocument { get; private set; } = null!;
    public string DuplicateKey { get; private set; } = string.Empty;
    public DateOnly? ValidUntil { get; private set; }
    public string? Currency { get; private set; }
    public int? DeliveryLeadTimeDays { get; private set; }
    public QuotationStatus Status { get; private set; }
    public long Version { get; private set; }
    public UserId? VerifiedBy { get; private set; }
    public DateTimeOffset? VerifiedAt { get; private set; }
    public string? RejectionReason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyList<QuotationLine> Lines => _lines.AsReadOnly();
    public IReadOnlyList<ExtractedField> Fields => _fields.AsReadOnly();

    private PoultryQuote() { }

    public static PoultryQuote Create(PurchaseRequestReference requestReference, SupplierReference supplier, SourceDocument document)
    {
        var now = DateTimeOffset.UtcNow;

        var quote = new PoultryQuote
        {
            Id = new PoultryQuoteId(Guid.NewGuid()),
            RequestReference = requestReference,
            Supplier = supplier,
            SourceDocument = document,
            DuplicateKey = $"{requestReference.RequestId}:{document.Sha256Hash}",
            Status = QuotationStatus.Uploaded,
            Version = 1,
            CreatedAt = now,
            UpdatedAt = now
        };

        quote.AddDomainEvent(new QuotationUploaded(quote.Id, requestReference.RequestId, now));

        return quote;
    }

    public void BeginExtraction()
    {
        if (Status is not (QuotationStatus.Uploaded or QuotationStatus.Rejected))
            throw new DomainException($"Cannot begin extraction from status '{Status}'.");

        Status = QuotationStatus.Processing;
        RejectionReason = null;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void ApplyExtraction(ExtractedQuotationData data)
    {
        if (Status != QuotationStatus.Processing)
            throw new DomainException($"Cannot apply extraction from status '{Status}'.");

        ValidUntil = data.ValidUntil;
        Currency = data.Currency;
        DeliveryLeadTimeDays = data.DeliveryLeadTimeDays;

        _lines.Clear();
        var lineNumber = 1;
        foreach (var lineData in data.Lines)
        {
            var line = QuotationLine.Create(lineNumber++, lineData.Description, lineData.Quantity, lineData.UnitOfMeasure, lineData.UnitPrice);

            foreach (var specification in lineData.Specifications)
                line.AddSpecification(specification);

            _lines.Add(line);
        }

        _fields.Clear();
        var requiredPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "validUntil",
            "currency",
            "deliveryLeadTimeDays"
        };
        for (var index = 0; index < data.Lines.Count; index++)
        {
            requiredPaths.Add($"lines[{index}].description");
            requiredPaths.Add($"lines[{index}].quantity");
            requiredPaths.Add($"lines[{index}].unitOfMeasure");
            requiredPaths.Add($"lines[{index}].unitPrice");
        }

        foreach (var fieldData in data.Fields)
        {
            var field = ExtractedField.Create(
                fieldData.FieldPath,
                fieldData.Value,
                requiredPaths.Contains(fieldData.FieldPath),
                new ConfidenceScore(fieldData.Confidence),
                new SourceReference(fieldData.PageNumber, fieldData.TextReference),
                fieldData.IsResolved);

            _fields.Add(field);
        }

        foreach (var missingPath in requiredPaths.Where(path => _fields.All(field => !field.FieldPath.Equals(path, StringComparison.OrdinalIgnoreCase))))
        {
            _fields.Add(ExtractedField.Create(
                missingPath,
                null,
                true,
                new ConfidenceScore(0),
                new SourceReference(1, "Field not located in the source document."),
                false));
        }

        Status = QuotationStatus.RequiresVerification;
        Version++;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void CorrectField(ExtractedFieldId fieldId, string value, UserId author, string reason)
    {
        if (Status != QuotationStatus.RequiresVerification)
            throw new DomainException($"Cannot correct fields in quotation status '{Status}'.");

        var field = _fields.FirstOrDefault(f => f.Id == fieldId)
            ?? throw new DomainException($"Extracted field '{fieldId}' was not found in this quotation.");

        ApplyCorrectedValue(field.FieldPath, value);
        field.Correct(value, author, reason);
        Version++;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Confirm(UserId confirmedBy, IReadOnlyDictionary<Guid, string> lineMappings)
    {
        if (Status != QuotationStatus.RequiresVerification)
            throw new DomainException($"Cannot confirm a quotation in status '{Status}'.");

        if (HasUnresolvedRequiredFields())
            throw new DomainException("Cannot confirm a quotation with unresolved required fields.");

        if (ValidUntil is null || string.IsNullOrWhiteSpace(Currency) || DeliveryLeadTimeDays is null)
            throw new DomainException("Quotation header data is incomplete.");

        if (_lines.Count == 0 || _lines.Any(line => !line.IsComplete()))
            throw new DomainException("Every quotation line must contain description, quantity, unit, and price before confirmation.");

        foreach (var line in _lines)
        {
            if (!lineMappings.TryGetValue(line.Id.Value, out var requestedItemId))
                throw new DomainException($"Quotation line '{line.Id}' must be linked to a requested item.");

            line.LinkToRequestedItem(requestedItemId);
        }

        foreach (var field in _fields.Where(field => field.Status == FieldResolutionStatus.Resolved))
            field.Confirm();

        Status = QuotationStatus.Verified;
        VerifiedBy = confirmedBy;
        VerifiedAt = DateTimeOffset.UtcNow;
        Version++;
        UpdatedAt = VerifiedAt.Value;

        AddDomainEvent(new QuotationVerified(Id, RequestReference.RequestId, Version, UpdatedAt));
    }

    public void Reject(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("Rejection reason is required.");

        Status = QuotationStatus.Rejected;
        RejectionReason = reason;
        Version++;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public bool HasUnresolvedRequiredFields() =>
        _fields.Any(field => field.IsRequired && field.Status == FieldResolutionStatus.Unresolved);

    private void ApplyCorrectedValue(string fieldPath, string value)
    {
        switch (fieldPath)
        {
            case "validUntil":
                if (!DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var validUntil))
                    throw new DomainException("Corrected validity date must use YYYY-MM-DD format.");
                ValidUntil = validUntil;
                return;
            case "currency":
                if (value.Trim().Length != 3 || !value.All(char.IsLetter))
                    throw new DomainException("Corrected currency must be a three-letter ISO code.");
                Currency = value.Trim().ToUpperInvariant();
                return;
            case "deliveryLeadTimeDays":
                if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var days) || days < 0)
                    throw new DomainException("Corrected delivery lead time must be a non-negative integer.");
                DeliveryLeadTimeDays = days;
                return;
        }

        var match = LineFieldPath.Match(fieldPath);
        if (!match.Success)
            return;

        var index = int.Parse(match.Groups["index"].Value, CultureInfo.InvariantCulture);
        if (index < 0 || index >= _lines.Count)
            throw new DomainException($"Quotation line index '{index}' does not exist.");

        var line = _lines[index];
        switch (match.Groups["field"].Value)
        {
            case "description":
                line.CorrectDescription(value);
                break;
            case "quantity":
                line.CorrectQuantity(ParseDecimal(value, "quantity"));
                break;
            case "unitOfMeasure":
                line.CorrectUnitOfMeasure(value);
                break;
            case "unitPrice":
                line.CorrectUnitPrice(ParseDecimal(value, "unit price"));
                break;
        }
    }

    private static decimal ParseDecimal(string value, string field)
    {
        if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
            throw new DomainException($"Corrected {field} must be numeric using invariant format.");
        return parsed;
    }
}
