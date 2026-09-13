using SmartQuote.API.Shared.Domain;
using SmartQuote.API.Shared.Application.Security;
using SmartQuote.API.Shared.Domain.Model.ValueObjects;
using SmartQuote.API.EvaluationSimulation.Application.OutboundServices;
using SmartQuote.API.PurchaseOrdering.Application.Ports;
using SmartQuote.API.PurchaseOrdering.Application.Views;
using SmartQuote.API.PurchaseOrdering.Domain.Model.Aggregates;
using SmartQuote.API.PurchaseOrdering.Domain.Model.Commands;
using SmartQuote.API.PurchaseOrdering.Domain.Model.ValueObjects;
using SmartQuote.API.PurchaseOrdering.Domain.Services;

namespace SmartQuote.API.PurchaseOrdering.Application;

public class PurchaseOrderApplicationService(
    IPurchaseOrderRepository repository,
    ISimulationDecisionReader decisionReader,
    ApprovedDecisionMapper decisionMapper,
    PurchaseOrderGenerator orderGenerator,
    IOrderNumberGenerator orderNumberGenerator,
    IPurchaseOrderingUnitOfWork unitOfWork,
    IDomainEventDispatcher domainEventDispatcher,
    ICurrentUser currentUser)
{
    public async Task<PurchaseOrderGenerationResult> ApproveAndGenerateAsync(ApproveAndGenerateCommand command, CancellationToken cancellationToken = default)
    {
        var idempotencyKey = $"{command.SimulationRunId}:{command.QuotationId}";

        var existing = await repository.FindByIdempotencyKeyAsync(idempotencyKey, cancellationToken);
        if (existing is not null)
            return new PurchaseOrderGenerationResult(ToView(existing), false);

        var existingForSimulation = await repository.FindBySimulationAsync(command.SimulationRunId.ToString(), cancellationToken);
        if (existingForSimulation is not null)
        {
            if (existingForSimulation.SourceDecision.QuotationId != command.QuotationId)
                throw new ConflictException("The simulation run already produced an order for a different quotation.");

            return new PurchaseOrderGenerationResult(ToView(existingForSimulation), false);
        }

        var snapshot = await decisionReader.GetApprovedSnapshotAsync(command.SimulationRunId, command.QuotationId, cancellationToken)
            ?? throw new NotFoundException($"No approved simulation decision was found for run '{command.SimulationRunId}' and quotation '{command.QuotationId}'.");

        var decision = decisionMapper.Map(snapshot, command.DeliveryConditions, command.DeliveryDestination);
        var approval = new Approval(new UserId(currentUser.UserId), DateTimeOffset.UtcNow, idempotencyKey);
        var orderNumber = await orderNumberGenerator.NextAsync(cancellationToken);

        var order = orderGenerator.Generate(decision, approval, orderNumber);

        await repository.AddAsync(order, cancellationToken);
        await unitOfWork.CompleteAsync(cancellationToken);

        await domainEventDispatcher.DispatchAsync(order.DomainEvents, cancellationToken);
        order.ClearDomainEvents();

        return new PurchaseOrderGenerationResult(ToView(order), true);
    }

    public async Task<PurchaseOrderView> GetByIdAsync(Guid purchaseOrderId, CancellationToken cancellationToken = default)
    {
        var order = await repository.GetByIdAsync(new PurchaseOrderId(purchaseOrderId), cancellationToken)
            ?? throw new NotFoundException($"Purchase order '{purchaseOrderId}' was not found.");

        return ToView(order);
    }

    public async Task<PurchaseOrderView> GetBySimulationAsync(string simulationRunId, CancellationToken cancellationToken = default)
    {
        var order = await repository.FindBySimulationAsync(simulationRunId, cancellationToken)
            ?? throw new NotFoundException($"No purchase order was found for simulation run '{simulationRunId}'.");

        return ToView(order);
    }

    private static PurchaseOrderView ToView(PurchaseOrder order) => new(
        order.Id,
        order.OrderNumber.Value,
        order.SourceDecision.SimulationRunId,
        order.SourceDecision.PurchaseRequestId,
        order.SourceDecision.QuotationId,
        order.SourceDecision.InputFingerprint,
        order.Supplier.SupplierId,
        order.Supplier.BusinessName,
        order.Supplier.TaxIdentifier,
        order.Approval.ApprovedBy,
        order.Approval.ApprovedAt,
        order.Status.ToString(),
        order.Currency,
        order.DeliveryTerms.LeadTimeDays,
        order.DeliveryTerms.Conditions,
        order.DeliveryTerms.Destination,
        order.CalculateTotal().Amount,
        order.CreatedAt,
        order.Lines.Select(line => new PurchaseOrderLineView(
            line.Id,
            line.LineNumber,
            line.SourceQuotationLineId,
            line.SourceRequestedItemId,
            line.Description,
            line.Quantity,
            line.UnitOfMeasure,
            line.UnitPrice)).ToList());
}
