using SmartQuote.Modules.QuotationIntake.Application.Views;
using SmartQuote.Modules.QuotationIntake.Interfaces.REST.Resource;

namespace SmartQuote.Modules.QuotationIntake.Interfaces.REST.Transform;

public static class PoultryQuoteResourceFromViewAssembler
{
    public static PoultryQuoteResource ToResource(PoultryQuoteView view) =>
        new(
            view.QuotationId,
            view.RequestId,
            view.SupplierId,
            view.SupplierBusinessName,
            view.SupplierTaxIdentifier,
            view.FileName,
            view.ValidUntil,
            view.Currency,
            view.DeliveryLeadTimeDays,
            view.Status,
            view.Version,
            view.VerifiedBy,
            view.VerifiedAt,
            view.RejectionReason,
            view.CreatedAt,
            view.UpdatedAt,
            view.Lines.Select(ToLineResource).ToList(),
            view.Fields.Select(ToFieldResource).ToList());

    private static QuotationLineResource ToLineResource(QuotationLineView view) =>
        new(
            view.LineId,
            view.RequestedItemId,
            view.LineNumber,
            view.Description,
            view.Quantity,
            view.UnitOfMeasure,
            view.UnitPrice,
            view.Specifications.Select(spec => new QuotedSpecificationResource(spec.Name, spec.Value, spec.UnitOfMeasure)).ToList());

    private static ExtractedFieldResource ToFieldResource(ExtractedFieldView view) =>
        new(
            view.FieldId,
            view.FieldPath,
            view.OriginalValue,
            view.CurrentValue,
            view.IsRequired,
            view.Confidence,
            view.SourcePageNumber,
            view.SourceTextReference,
            view.Status,
            view.Corrections.Select(correction => new FieldCorrectionResource(
                correction.PreviousValue,
                correction.CorrectedValue,
                correction.CorrectedBy,
                correction.CorrectedAt,
                correction.Reason)).ToList());
}
