using SmartQuote.API.SupplyRequests.Application.OutboundServices;
using SmartQuote.Modules.EvaluationSimulation.Domain.Model.ValueObjects;
using SmartQuote.Modules.QuotationIntake.Application.OutboundServices;
using DomainComparisonOperator = SmartQuote.Modules.EvaluationSimulation.Domain.Model.Enums.ComparisonOperator;

namespace SmartQuote.Modules.EvaluationSimulation.Application;

/// <summary>
/// Anti-corruption mapper: translates the imported DTOs from SupplyRequests and QuotationIntake
/// into this context's own ubiquitous language (docs/3-evaluation-simulation.puml).
/// </summary>
public class EvaluationInputAssembler
{
    public EvaluationDataset Assemble(PurchaseRequestSnapshot request, IReadOnlyList<VerifiedQuotationSnapshot> quotations)
    {
        var capturedAt = DateTimeOffset.UtcNow;

        var requestSnapshot = new RequestEvaluationSnapshot(
            request.RequestId,
            request.Version,
            request.RequiredDate,
            request.Priority,
            request.Items.Select(item => new RequestItemSnapshotData(
                item.ItemId,
                item.LineNumber,
                item.Description,
                item.Quantity,
                item.UnitOfMeasure,
                item.Requirements.Select(requirement => new RequestRequirementSnapshotData(
                    requirement.RequirementId,
                    requirement.Name,
                    Enum.Parse<DomainComparisonOperator>(requirement.Operator, ignoreCase: true),
                    requirement.ExpectedValue,
                    requirement.UnitOfMeasure,
                    requirement.IsMandatory)).ToList())).ToList(),
            capturedAt);

        var quotationSnapshots = quotations.Select(quotation => new QuotationEvaluationSnapshot(
            quotation.QuotationId,
            quotation.Version,
            quotation.SupplierId,
            quotation.SupplierBusinessName,
            quotation.SupplierTaxIdentifier,
            quotation.Currency,
            quotation.DeliveryLeadTimeDays,
            quotation.VerifiedAt,
            quotation.Lines.Select(line => new QuotationLineSnapshotData(
                line.LineId,
                line.RequestedItemId,
                line.LineNumber,
                line.Description,
                line.Quantity,
                line.UnitOfMeasure,
                line.UnitPrice,
                line.Specifications.Select(spec => new QuotationSpecificationSnapshotData(spec.Name, spec.Value, spec.UnitOfMeasure)).ToList())).ToList(),
            capturedAt)).ToList();

        return new EvaluationDataset(requestSnapshot, quotationSnapshots);
    }
}
