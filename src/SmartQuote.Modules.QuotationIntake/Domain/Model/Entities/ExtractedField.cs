using SmartQuote.API.Shared.Domain;
using SmartQuote.API.Shared.Domain.Model.ValueObjects;
using SmartQuote.Modules.QuotationIntake.Domain.Model.Enums;
using SmartQuote.Modules.QuotationIntake.Domain.Model.ValueObjects;

namespace SmartQuote.Modules.QuotationIntake.Domain.Model.Entities;

public class ExtractedField
{
    private readonly List<FieldCorrection> _corrections = [];

    public ExtractedFieldId Id { get; private set; } = null!;
    public string FieldPath { get; private set; } = string.Empty;
    public string? OriginalValue { get; private set; }
    public string? CurrentValue { get; private set; }
    public bool IsRequired { get; private set; }
    public ConfidenceScore Confidence { get; private set; } = null!;
    public SourceReference Source { get; private set; } = null!;
    public FieldResolutionStatus Status { get; private set; }

    public IReadOnlyList<FieldCorrection> Corrections => _corrections.AsReadOnly();

    private ExtractedField() { }

    private ExtractedField(
        ExtractedFieldId id,
        string fieldPath,
        string? value,
        bool isRequired,
        ConfidenceScore confidence,
        SourceReference source,
        FieldResolutionStatus status)
    {
        Id = id;
        FieldPath = fieldPath;
        OriginalValue = value;
        CurrentValue = value;
        IsRequired = isRequired;
        Confidence = confidence;
        Source = source;
        Status = status;
    }

    public static ExtractedField Create(
        string fieldPath,
        string? value,
        bool isRequired,
        ConfidenceScore confidence,
        SourceReference source,
        bool isResolvedByAgent)
    {
        if (string.IsNullOrWhiteSpace(fieldPath))
            throw new DomainException("Extracted field path is required.");

        var status = string.IsNullOrWhiteSpace(value) || !isResolvedByAgent
            ? FieldResolutionStatus.Unresolved
            : FieldResolutionStatus.Resolved;

        return new ExtractedField(new ExtractedFieldId(Guid.NewGuid()), fieldPath, value, isRequired, confidence, source, status);
    }

    public void MarkUnresolved() => Status = FieldResolutionStatus.Unresolved;

    public void Confirm()
    {
        if (Status == FieldResolutionStatus.Unresolved)
            throw new DomainException($"Field '{FieldPath}' cannot be confirmed while unresolved.");

        Status = FieldResolutionStatus.Confirmed;
    }

    public void Correct(string value, UserId author, string reason)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("Corrected value is required.");

        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("Correction reason is required.");

        _corrections.Add(FieldCorrection.Create(CurrentValue ?? string.Empty, value, author, reason));
        CurrentValue = value;
        Status = FieldResolutionStatus.Corrected;
    }
}
