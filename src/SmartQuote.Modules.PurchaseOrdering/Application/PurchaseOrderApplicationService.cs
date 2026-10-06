using SmartQuote.API.PurchaseOrdering.Application.Ports;
using SmartQuote.API.PurchaseOrdering.Application.Views;
using SmartQuote.API.PurchaseOrdering.Domain.Model.Aggregates;
using SmartQuote.API.PurchaseOrdering.Domain.Model.Enums;
using SmartQuote.API.PurchaseOrdering.Domain.Model.Commands;
using SmartQuote.API.PurchaseOrdering.Domain.Model.ValueObjects;
using SmartQuote.API.PurchaseOrdering.Domain.Services;
using SmartQuote.API.Shared.Application.Security;
using SmartQuote.API.Shared.Domain;
using SmartQuote.API.Shared.Domain.Model.ValueObjects;
using SmartQuote.Modules.EvaluationSimulation.Application.OutboundServices;

namespace SmartQuote.Modules.PurchaseOrdering.Application;

public class PurchaseOrderApplicationService(
    IPurchaseOrderRepository repository,
    ISimulationDecisionReader decisionReader,
    ApprovedDecisionMapper decisionMapper,
    PurchaseOrderGenerator orderGenerator,
    IOrderNumberGenerator orderNumberGenerator,
    IPurchaseOrderingUnitOfWork unitOfWork,
    IOrderRequestLifecycle requestLifecycle,
    IDomainEventDispatcher domainEventDispatcher,
    IDeliveryEvaluationRepository deliveryEvaluations,
    ICurrentUser currentUser)
{
    public async Task<PurchaseOrderGenerationResult> ApproveAndGenerateAsync(ApproveAndGenerateCommand command, CancellationToken cancellationToken = default)
    {
        var idempotencyKey = $"{command.SimulationRunId}:{command.QuotationId}";

        var existing = await repository.FindByIdempotencyKeyAsync(idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            await requestLifecycle.EnsureOrderedAsync(existing.SourceDecision.PurchaseRequestId, existing.OrderNumber.Value, cancellationToken);
            return new PurchaseOrderGenerationResult(ToView(existing), false);
        }

        var existingForSimulation = await repository.FindBySimulationAsync(command.SimulationRunId.ToString(), cancellationToken);
        if (existingForSimulation is not null)
        {
            if (existingForSimulation.SourceDecision.QuotationId != command.QuotationId)
                throw new ConflictException("The simulation run already produced an order for a different quotation.");

            await requestLifecycle.EnsureOrderedAsync(existingForSimulation.SourceDecision.PurchaseRequestId, existingForSimulation.OrderNumber.Value, cancellationToken);
            return new PurchaseOrderGenerationResult(ToView(existingForSimulation), false);
        }

        var snapshot = await decisionReader.GetApprovedSnapshotAsync(command.SimulationRunId, command.QuotationId, cancellationToken)
            ?? throw new NotFoundException($"No approved simulation decision was found for run '{command.SimulationRunId}' and quotation '{command.QuotationId}'.");

        var existingForRequest = await repository.FindByRequestAsync(snapshot.RequestId, cancellationToken);
        if (existingForRequest is not null)
            throw new ConflictException("This purchase request already has an approved purchase order.");

        var decision = decisionMapper.Map(snapshot, command.DeliveryConditions, command.DeliveryDestination);
        var approval = new Approval(new UserId(currentUser.UserId), DateTimeOffset.UtcNow, idempotencyKey);
        var orderNumber = await orderNumberGenerator.NextAsync(cancellationToken);

        var order = orderGenerator.Generate(decision, approval, orderNumber);

        await repository.AddAsync(order, cancellationToken);
        await unitOfWork.CompleteAsync(cancellationToken);

        await domainEventDispatcher.DispatchAsync(order.DomainEvents, cancellationToken);
        order.ClearDomainEvents();

        await requestLifecycle.EnsureOrderedAsync(snapshot.RequestId, order.OrderNumber.Value, cancellationToken);

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

    public async Task<PurchaseOrderView?> GetByRequestAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var order = await repository.FindByRequestAsync(requestId.ToString(), cancellationToken);
        return order is null ? null : ToView(order);
    }

    public async Task<PurchaseOrderView> MarkDeliveredAsync(Guid purchaseOrderId, CancellationToken cancellationToken = default)
    {
        var order = await repository.GetByIdAsync(new PurchaseOrderId(purchaseOrderId), cancellationToken)
            ?? throw new NotFoundException($"Purchase order '{purchaseOrderId}' was not found.");

        order.MarkDelivered(new UserId(currentUser.UserId), DateTimeOffset.UtcNow);
        await unitOfWork.CompleteAsync(cancellationToken);

        await domainEventDispatcher.DispatchAsync(order.DomainEvents, cancellationToken);
        order.ClearDomainEvents();

        return ToView(order);
    }

    public async Task<DeliveryEvaluationView> EvaluateDeliveryAsync(
        Guid purchaseOrderId,
        int onTimeScore,
        int qualityScore,
        string? observations,
        CancellationToken cancellationToken = default)
    {
        var order = await repository.GetByIdAsync(new PurchaseOrderId(purchaseOrderId), cancellationToken)
            ?? throw new NotFoundException($"Purchase order '{purchaseOrderId}' was not found.");

        if (order.Status != PurchaseOrderStatus.Delivered)
            throw new DomainException("Only delivered purchase orders can be evaluated.");

        if (await deliveryEvaluations.ExistsForOrderAsync(order.Id, cancellationToken))
            throw new ConflictException("This purchase order already has a delivery evaluation.");

        var evaluation = DeliveryEvaluation.Create(
            order.Id,
            order.Supplier.TaxIdentifier,
            onTimeScore,
            qualityScore,
            observations,
            new UserId(currentUser.UserId),
            DateTimeOffset.UtcNow);

        await deliveryEvaluations.AddAsync(evaluation, cancellationToken);
        await unitOfWork.CompleteAsync(cancellationToken);

        return ToDeliveryEvaluationView(evaluation);
    }

    public async Task<SupplierPerformanceView> GetSupplierPerformanceAsync(string taxIdentifier, CancellationToken cancellationToken = default)
    {
        var evaluations = await deliveryEvaluations.ListBySupplierAsync(taxIdentifier, cancellationToken);
        if (evaluations.Count == 0)
            return new SupplierPerformanceView(taxIdentifier, 0, null, null, null, null, null, []);

        var onTime = (decimal)evaluations.Average(evaluation => evaluation.OnTimeScore);
        var quality = (decimal)evaluations.Average(evaluation => evaluation.QualityScore);

        return new SupplierPerformanceView(
            taxIdentifier,
            evaluations.Count,
            onTime,
            quality,
            (onTime + quality) / 2,
            evaluations.Min(evaluation => evaluation.EvaluatedAt),
            evaluations.Max(evaluation => evaluation.EvaluatedAt),
            evaluations.OrderByDescending(evaluation => evaluation.EvaluatedAt)
                .ThenBy(evaluation => evaluation.Id.Value)
                .Select(ToDeliveryEvaluationView).ToList());
    }

    private static DeliveryEvaluationView ToDeliveryEvaluationView(DeliveryEvaluation evaluation) => new(
        evaluation.Id,
        evaluation.PurchaseOrderId,
        evaluation.SupplierTaxIdentifier,
        evaluation.OnTimeScore,
        evaluation.QualityScore,
        evaluation.Observations,
        evaluation.EvaluatedBy.Value,
        evaluation.EvaluatedAt);

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
