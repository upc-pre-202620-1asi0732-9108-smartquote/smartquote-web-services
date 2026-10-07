using SmartQuote.API.Shared.Domain.Model.ValueObjects;

namespace SmartQuote.Modules.QuotationIntake.Domain.Model.Entities;

public class FieldCorrection
{
    public string PreviousValue { get; private set; } = string.Empty;
    public string CorrectedValue { get; private set; } = string.Empty;
    public UserId CorrectedBy { get; private set; } = null!;
    public DateTimeOffset CorrectedAt { get; private set; }
    public string Reason { get; private set; } = string.Empty;

    private FieldCorrection() { }

    private FieldCorrection(string previousValue, string correctedValue, UserId correctedBy, string reason)
    {
        PreviousValue = previousValue;
        CorrectedValue = correctedValue;
        CorrectedBy = correctedBy;
        CorrectedAt = DateTimeOffset.UtcNow;
        Reason = reason;
    }

    public static FieldCorrection Create(string previousValue, string correctedValue, UserId correctedBy, string reason) =>
        new(previousValue, correctedValue, correctedBy, reason);
}
