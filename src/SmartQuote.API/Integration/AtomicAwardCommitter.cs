using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SmartQuote.API.PurchaseOrdering.Application.Ports;
using SmartQuote.API.PurchaseOrdering.Domain.Model.Entities;
using SmartQuote.API.PurchaseOrdering.Infrastructure.Persistence.EFC.Configuration;
using SmartQuote.API.Shared.Application.Security;
using SmartQuote.API.Shared.Domain;
using SmartQuote.API.Shared.Domain.Model.ValueObjects;
using SmartQuote.API.SupplyRequests.Application.Ports;
using SmartQuote.API.SupplyRequests.Domain.Model.Enums;
using SmartQuote.API.SupplyRequests.Domain.Model.ValueObjects;
using SmartQuote.API.SupplyRequests.Infrastructure.Persistence.EFC.Configuration;
using SmartQuote.Modules.EvaluationSimulation.Infrastructure.Persistence.EFC.Configuration;
using SmartQuote.Modules.QuotationIntake.Infrastructure.Persistence.EFC.Configuration;

namespace SmartQuote.API.Integration;

/// <summary>US21 transaction boundary: only infrastructure knows both EF contexts.</summary>
public sealed class AtomicAwardCommitter(PurchaseOrderingDbContext ordering, SupplyRequestsDbContext supply,
    IPurchaseRequestRepository requests, ICurrentUser user, OrderingOutboxDispatcher dispatcher,
    EvaluationSimulationDbContext evaluation, QuotationIntakeDbContext quotation) : IAtomicAwardCommitter
{
    public async Task PrepareAsync(string requestId, string reference, IReadOnlyList<IDomainEvent> orderEvents,
        Func<CancellationToken, Task> validateLockedDecision, CancellationToken ct)
    {
        var transaction = ordering.Database.CurrentTransaction ?? throw new InvalidOperationException("An active award transaction is required.");
        supply.Database.SetDbConnection(ordering.Database.GetDbConnection(), contextOwnsConnection: false);
        await supply.Database.UseTransactionAsync(transaction.GetDbTransaction(), ct);
        var id = Guid.Parse(requestId);
        // Serialize against ordinary status changes as well as concurrent award requests.
        await supply.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM supply_requests.purchase_requests WHERE id = {id} FOR UPDATE", ct);
        await ordering.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM evaluation_simulation.evaluation_scenarios WHERE purchase_request_id = {id} ORDER BY id FOR SHARE", ct);
        await ordering.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM quotation_intake.poultry_quotes WHERE purchase_request_id = {id} ORDER BY id FOR SHARE", ct);
        supply.ChangeTracker.Clear();
        evaluation.ChangeTracker.Clear(); quotation.ChangeTracker.Clear();
        await validateLockedDecision(ct);
        var request = await requests.GetByIdAsync(new PurchaseRequestId(id), ct) ?? throw new NotFoundException("Purchase request was not found.");
        if (request.Status == RequestStatus.Evaluation) request.ChangeStatus(RequestStatus.Approved, new UserId(user.UserId), $"Purchase award {reference} approved.");
        if (request.Status != RequestStatus.Approved) throw new DomainException("Only an evaluated or approved request can receive an award.");
        request.ChangeStatus(RequestStatus.Ordered, new UserId(user.UserId), $"Purchase award {reference} issued.");
        foreach (var e in orderEvents.Concat(request.DomainEvents))
            ordering.OrderingOutbox.Add(new OrderingOutboxMessage { Id = Guid.NewGuid(), EventType = e.GetType().Name,
                Payload = JsonSerializer.Serialize(e, e.GetType()), CreatedAt = DateTimeOffset.UtcNow });
        await supply.SaveChangesAsync(ct);
        request.ClearDomainEvents();
    }
    public async Task AfterCommitAsync(CancellationToken ct)
    {
        await supply.Database.UseTransactionAsync(null, ct);
        // Delivery failure never reverses an already committed award. Durable messages remain for the worker.
        await dispatcher.TryDeliverAsync(ct);
    }
}
